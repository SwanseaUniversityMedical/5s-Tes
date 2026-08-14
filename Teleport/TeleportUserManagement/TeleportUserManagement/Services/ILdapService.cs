using System.DirectoryServices.Protocols;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FiveSafesTes.Core.Services;
using Novell.Directory.Ldap;
using TeleportUserManagement.Models;
using TeleportUserManagement.Models.Settings;
using TeleportUserManagement.Utilities;
using ILogger = Serilog.ILogger;
using LdapConnection = Novell.Directory.Ldap.LdapConnection;
using LdapException = Novell.Directory.Ldap.LdapException;

namespace TeleportUserManagement.Services
{
    public interface ILdapService
    {
        Task<ResultType> CreateUserAccount(string userName, string givenName, string surname, string email,
            string description, bool enabled, bool requirePasswordChange, bool passwordNeverExpires,
            List<string> ouPath, string passwordOverride = "");
        LdapUser FindUserByIdentityDefault(string userName);
        bool CheckUserExists(string userName);
        bool CheckGroupExists(string groupName);
        ResultType CreateGroup(string groupName, string description, List<string> ouPath, bool multidomains = false);
        void AddUserToGroup(string userName, string groupName);
        void RemoveUserFromGroup(string username, string groupName);
        List<string> GetGroupMemberUsernames(string groupName);
    }

    public class LdapService : ILdapService
    {
        private readonly IVaultCredentialsService _vaultCredentialsService;
        private readonly ILogger log;
        private readonly LdapConnection ldapConnection;
        private readonly string domainNameDcFormat;
        private readonly string machineName;

        private readonly string domain;
        private readonly string shortDomain;
        private readonly string username;
        private readonly string password;
        private readonly bool useSsl;
        private readonly List<string> _ouPath;

        private string userOu;
        private string groupOu;

        public LdapService(ActiveDirectorySettings adSettings, IVaultCredentialsService vaultCredsService)
        {
            _vaultCredentialsService = vaultCredsService;

            useSsl = adSettings.Connection.UseSsl;
            log = Serilog.Log.ForContext<LdapService>();

            domain = adSettings.Connection.Domain;
            var segments = domain.Split('.');
            domainNameDcFormat = "";
            foreach (var segment in segments)
            {
                if (domainNameDcFormat != "")
                {
                    domainNameDcFormat += ",";
                }

                domainNameDcFormat += "DC=" + segment;
            }

            shortDomain = adSettings.Behaviour.ShortDomain;

            username = adSettings.Connection.Username;
            password = adSettings.Connection.Password;

            machineName = adSettings.Connection.Machine;
            _ouPath = adSettings.Connection.BaseOu.Split(',').ToList();
            ldapConnection = GetLdapConnection();
        }

        #region Setup

        private LdapConnection GetLdapConnection()
        {
            var options = new LdapConnectionOptions();
            options.ConfigureRemoteCertificateValidationCallback(RemoteCertificateValidation);

            var connection = new LdapConnection(options);

            try
            {
                // Build the distinguished name (DN)
                log.Information("{Function} Connecting to {MachineName}", nameof(GetLdapConnection), machineName);

                //Dc needs plain ldap. We need ldaps. Possible issues down the line
                if (!useSsl)
                {
                    connection.SecureSocketLayer = false; // disable SSL
                    connection.ConnectAsync(machineName, 389).Wait(); // Use 389 for plain LDAP
                }
                else
                {
                    connection.SecureSocketLayer = true; // enable SSL
                    connection.ConnectAsync(machineName, 636).Wait(); // Use 636 for LDAP
                }

                // Username should be the full DN or UPN depending on your server
                connection.BindAsync(username, password).Wait();
                var cons = connection.SearchConstraints;
                cons.ReferralFollowing = true;
                connection.Constraints = cons;

                return connection;
            }
            catch (LdapException ex)
            {
                log.Error(ex, "LDAP bind failed");
                throw;
            }
        }

        private static bool RemoteCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return sslPolicyErrors == SslPolicyErrors.None;
        }

        #endregion

        #region User Utility

        public LdapUser FindUserByIdentityDefault(string userName)
        {
            return FindUserByIdentityGuts(userName, "", "", _ouPath);
        }

        public bool CheckUserExists(string userName)
        {
            return FindUserByIdentityDefault(userName) != null;
        }

        private LdapUser FindUserByIdentityGuts(string userName, string domainName, string otherdomainou = "", List<string> ouPath = null)
        {
            return GetUserFromUsernameUsingLdap(userName, domainName, otherdomainou, ouPath);
        }

        private LdapUser GetUserFromUsernameUsingLdap(string userName, string domainName, string otherdomainou, List<string> ouPath)
        {
            var baseOu = GetOuForUser(otherdomainou, ouPath);

            try
            {
                var strippedUserName = StripDomain(userName);
                var safeUserName = LdapFilterEscape(strippedUserName);
                var filter = BuildUserSearchFilter(safeUserName);
                var results = LdapUserSearching(filter, baseOu, domainName);

                return GetFirstUserFromSearch(results.Entries);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("The LDAP server is unavailable"))
                {
                    log.Warning(ex, "{Function} LDAP search failed for user: {UserName}", nameof(FindUserByIdentityGuts), userName);
                }
                else
                {
                    log.Error(ex, "{Function} LDAP search failed for user: {UserName}", nameof(FindUserByIdentityGuts), userName);
                }

                return null;
            }
        }

        private string GetOuForUser(string otherdomainou, List<string> ouPath)
        {
            if (!string.IsNullOrWhiteSpace(otherdomainou)) return otherdomainou;

            if (ouPath != null)
            {
                GetUserOu(ouPath);
            }

            return userOu;
        }


        private static string StripDomain(string userName)
        {
            return userName.Contains('\\') ? userName[(userName.IndexOf('\\') + 1)..] : userName;
        }

        private static string LdapFilterEscape(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            // Escapes special characters for an LDAP filter
            return input
                .Replace("\\", "\\5c")
                .Replace("*", "\\2a")
                .Replace("(", "\\28")
                .Replace(")", "\\29")
                .Replace("\0", "\\00");
        }

        private static string BuildUserSearchFilter(string safeUserName)
        {
            return $"(&(|(sAMAccountName={safeUserName})(userPrincipalName={safeUserName}))(objectClass=user)(objectCategory=person))";
        }

        private SearchResponse LdapUserSearching(string filter, string searchPath, string domainName)
        {
            var identifier = new LdapDirectoryIdentifier(domainName, useSsl ? 636 : 389);
            using var ldapConnection = new System.DirectoryServices.Protocols.LdapConnection(identifier);

            ldapConnection.SessionOptions.SecureSocketLayer = useSsl;

            var hasDomain = username.Split('\\');
            var nameToUse = username;
            if (hasDomain.Length > 1)
            {
                nameToUse = hasDomain[1];
            }

            var credentials = new NetworkCredential(nameToUse, password, shortDomain);
            ldapConnection.Credential = credentials;

            ldapConnection.AuthType = AuthType.Negotiate;
            ldapConnection.Bind();

            var request = new SearchRequest(searchPath, filter, SearchScope.Subtree);

            var response = (SearchResponse)ldapConnection.SendRequest(request);
            return response;
        }

        private static LdapUser GetFirstUserFromSearch(SearchResultEntryCollection searchResults)
        {
            return searchResults.Count > 0 ? new LdapUser(searchResults[0]) : null;
        }

        #endregion

        #region User Creation

        public async Task<ResultType> CreateUserAccount(string userName, string givenName, string surname, string email,
            string description, bool enabled, bool requirePasswordChange, bool passwordNeverExpires,
            List<string> ouPath, string passwordOverride = "")
        {
            if (CheckUserExists(userName))
            {
                return ResultType.Exists;
            }

            var password = PasswordGenerator.Generate();

            if (!string.IsNullOrWhiteSpace(passwordOverride))
            {
                password = passwordOverride;
            }

            // Add the password to vault
            bool vaultWriteSuccess = await _vaultCredentialsService.AddCredentialAsync($"passwords/{userName}", new() {{ "password", password }});

            if (!vaultWriteSuccess)
            {
                log.Error("{Function} Failed to write password to vault for {UserName}", "CreateUserAccount", userName);
            }

            GetUserOu(ouPath);

            var ldapou = userOu;

            var userPrincipleName = $"{userName}@{domain}";
            var userDn = $"CN={EscapeDnValue(userName)},{ldapou}";
            log.Information("{Function} Creating user {UserName}, {Email}, {GivenName}, {Description}, {PasswordNever}, {W2000Name}, {Post2000} {Enabled}",
                "CreateUserAccount", userName, email, givenName, description, passwordNeverExpires, userName, userPrincipleName, enabled);

            var attributes = new LdapAttributeSet
            {
                new LdapAttribute("objectClass", ["top", "person", "organizationalPerson", "user"]),
                new LdapAttribute("cn", userName),
                new LdapAttribute("sAMAccountName", userName),
                new LdapAttribute("userPrincipalName", userPrincipleName),
                new LdapAttribute("displayName", userName),
                new LdapAttribute("givenName", givenName),
                new LdapAttribute("mail", email)
            };

            // Only add the following values if they are not empty.
            if (!string.IsNullOrWhiteSpace(surname))
            {
                attributes.Add(new LdapAttribute("sn", surname));
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                attributes.Add(new LdapAttribute("description", description));
            }

            var newEntry = new LdapEntry(userDn, attributes);

            // Set the password
            var quotedPassword = $"\"{password}\"";
            var passwordBytes = Encoding.Unicode.GetBytes(quotedPassword);

            var pwdMod = new LdapModification(
                LdapModification.Replace,
                new LdapAttribute("unicodePwd", passwordBytes)
            );

            // Enable account (512 = NORMAL_ACCOUNT), disable = 514
            var userAccountControl = 512;
            if (!enabled) userAccountControl = 514;
            if (passwordNeverExpires) userAccountControl |= 0x10000;

            var uacMod = new LdapModification(
                LdapModification.Replace,
                new LdapAttribute("userAccountControl", userAccountControl.ToString())
            );

            var mods = new List<LdapModification> { pwdMod, uacMod };

            if (requirePasswordChange)
            {
                var pwdLastSetMod = new LdapModification(
                    LdapModification.Replace,
                    new LdapAttribute("pwdLastSet", "0")
                );
                mods.Add(pwdLastSetMod);
            }

            try
            {
                // Add the new user
                ldapConnection.AddAsync(newEntry).Wait();
                ldapConnection.ModifyAsync(userDn, mods.ToArray()).Wait();
            }
            catch (Exception ex)
            {
                log.Error(ex, "{Function} Failed to create user {UserName}", "CreateUserAccount", userName);
                return ResultType.Failure;
            }

            return ResultType.Success;
        }

        private void GetUserOu(List<string> ouPath)
        {
            userOu = GetBaseDn(ouPath);
        }

        private static string EscapeDnValue(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input
                .Replace("\\", @"\\")
                .Replace("\"", "\\\"")
                .Replace(",", "\\,")
                .Replace("+", "\\+")
                .Replace("<", "\\<")
                .Replace(">", "\\>")
                .Replace(";", "\\;")
                .Replace("=", "\\=");
        }

        #endregion

        #region Group Utility

        public ResultType CreateGroup(string groupName, string description, List<string> ouPath, bool multidomains = false)
        {

            if (CheckGroupExists(groupName))
            {
                return ResultType.Exists;
            }

            var ldapOu = GetOuForGroup(ouPath);
            var groupDn = $"CN={EscapeDnValue(groupName)},{ldapOu}";
            var attributes = new LdapAttributeSet
        {
            new LdapAttribute("objectClass", ["top", "group"]),
            new LdapAttribute("cn", groupName),
            new LdapAttribute("sAMAccountName", groupName)
        };

            if (!string.IsNullOrWhiteSpace(description))
            {
                attributes.Add(new LdapAttribute("description", description));
            }

            // Set groupType: security group + scope
            // Scope flags:
            // 0x00000002 = Global group
            // 0x00000004 = Domain local group
            // 0x00000008 = Universal group
            // Security flag:
            // 0x80000000 = Security-enabled
            var groupType = 0x80000002; // Global security group by default

            if (multidomains)
            {
                groupType = 0x80000004; // Domain local security group
            }

            attributes.Add(new LdapAttribute("groupType", groupType.ToString()));

            var groupEntry = new LdapEntry(groupDn, attributes);

            ldapConnection.AddAsync(groupEntry).Wait();

            return ResultType.Success;

        }

        public bool CheckGroupExists(string groupName)
        {

            return FindGroupDefault(groupName) != null;
        }

        private LdapGroup FindGroupDefault(string groupName)
        {
            return FindGroupGuts(groupName, _ouPath);
        }


        private LdapGroup FindGroupGuts(string groupName, List<string> ouPath = null)
        {
            var ldapOu = GetOuForGroup(ouPath);

            try
            {
                var filter = $"(&(objectClass=group)(cn={LdapFilterEscape(groupName)}))";

                var searchResults = ldapConnection.SearchAsync(ldapOu, LdapConnection.ScopeSub, filter, null, false)
                    .Result;

                while (searchResults.HasMoreAsync().Result)
                {
                    try
                    {
                        var entry = searchResults.NextAsync().Result;
                        return new LdapGroup(entry);
                    }
                    catch (AggregateException ae)
                    {
                        if (!AllReferrals(ae)) throw;
                    }
                }
            }
            catch (LdapException ex)
            {
                log.Error(ex, "FindGroup LDAP search failed for group: {GroupName}", groupName);
            }


            return null;

        }

        private string GetOuForGroup(List<string> ouPath)
        {
            GetGroupOu(ouPath);
            return groupOu;
        }

        private void GetGroupOu(List<string> ouPath)
        {
            groupOu = GetBaseDn(ouPath);
        }

        private static bool AllReferrals(AggregateException ae)
        {
            return ae.Flatten().InnerExceptions.All(ex => ex is LdapReferralException);
        }

        private bool IsUserMemberOfGroup(string userDn, string groupDn)
        {
            try
            {
                // Search the group to check if the user is in the 'member' attribute
                var searchResults = ldapConnection.SearchAsync(
                    groupDn,
                    LdapConnection.ScopeBase,
                    $"(member={EscapeLdapFilterValue(userDn)})",
                    ["dn"], // We're just interested in the DN here
                    false
                ).Result;
                if (searchResults.HasMoreAsync().Result)
                {
                    var entry = searchResults.NextAsync().Result;
                    return entry != null;
                }

                return false;

            }
            catch (AggregateException e)
            {
                log.Error(e, "{Function} Aggregate caught", "IsUserMemberOfGroup");
                foreach (var inner in e.InnerExceptions)
                {
                    log.Error(inner, "{Function} Aggregate inner error", "IsUserMemberOfGroup");

                }

                throw new InvalidOperationException(
                    $"Error while verifying LDAP group membership for user '{userDn}' in group '{groupDn}'.",
                    e);
            }
        }

        private static string EscapeLdapFilterValue(string value)
        {
            return value
                .Replace("\\", "\\5c")
                .Replace("*", "\\2a")
                .Replace("(", "\\28")
                .Replace(")", "\\29")
                .Replace("\0", "\\00");
        }

        #endregion

        #region User Groups

        public List<string> GetGroupMemberUsernames(string groupName)
        {
            var group = FindGroupDefault(groupName);
            if (group == null) return [];

            var usernames = new List<string>();

            try
            {
                // memberOf is AD's auto-maintained back-link for "member" - avoids parsing/unescaping DNs ourselves.
                var filter = $"(&(objectClass=user)(objectCategory=person)(memberOf={EscapeLdapFilterValue(group.DistinguishedName)}))";
                var searchResults = ldapConnection.SearchAsync(GetBaseDn(_ouPath), LdapConnection.ScopeSub, filter, ["sAMAccountName"], false).Result;

                while (searchResults.HasMoreAsync().Result)
                {
                    try
                    {
                        var entry = searchResults.NextAsync().Result;
                        var samAccountName = entry.GetStringValueOrDefault("sAMAccountName", null);
                        if (!string.IsNullOrEmpty(samAccountName)) usernames.Add(samAccountName);
                    }
                    catch (AggregateException ae)
                    {
                        if (!AllReferrals(ae)) throw;
                    }
                }
            }
            catch (LdapException ex)
            {
                log.Error(ex, "{Function} Error retrieving members for group {Group}", "GetGroupMemberUsernames", groupName);
            }

            return usernames;
        }


        public void AddUserToGroup(string userName, string groupName)
        {
            var user = FindUserByIdentityDefault(userName);

            var group = FindGroupDefault(groupName);

            if (user != null && group != null)
            {
                var userDn = user.DistinguishedName; // Get the user's distinguished name
                var groupDn = group.DistinguishedName; // Get the group's distinguished name

                try
                {
                    // Check if the user is already a member of the group
                    var isMember = IsUserMemberOfGroup(userDn, groupDn);
                    if (isMember)
                    {
                        log.Information("{Function} User {Username} is already a member of {Group}",
                            "AddUserToGroup", userDn, groupDn);
                        return;
                    }

                    // Create the LdapAttribute for the 'member' attribute to add the user's DN
                    var attribute = new LdapAttribute("member", userDn);

                    // Create the LdapModification to add the user to the 'member' attribute
                    var modification = new LdapModification(
                        LdapModification.Add, // Operation to add
                        attribute // The attribute to modify (member)
                    );

                    // Modify the group entry to include the user's DN in the 'member' attribute
                    ldapConnection.ModifyAsync(groupDn, [modification]).Wait();
                }
                catch (LdapException ex)
                {
                    log.Warning(ex, "{Function} Error adding user {User} to group {Group}.", "AddUserToGroup",
                        userName, groupName);
                }
            }
            else
            {
                log.Information("{Function} User {Username} exists = {UExists}, {Group} exists = {GExists}",
                    "AddUserToGroup", userName, user != null, groupName, group != null);
            }
        }

        public void RemoveUserFromGroup(string userName, string groupName)
        {
            var user = FindUserByIdentityDefault(userName);
            var group = FindGroupDefault(groupName);

            if (user != null && group != null)
            {
                var userDn = user.DistinguishedName;
                var groupDn = group.DistinguishedName;

                try
                {
                    var isMember = IsUserMemberOfGroup(userDn, groupDn);
                    if (!isMember)
                    {
                        log.Information("{Function} User {Username} is not a member of {Group}",
                            "RemoveUserFromGroup", userDn, groupDn);
                        return;
                    }

                    var attribute = new LdapAttribute("member", userDn);
                    var modification = new LdapModification(
                        LdapModification.Delete,
                        attribute
                    );

                    ldapConnection.ModifyAsync(groupDn, [modification]).Wait();
                }
                catch (LdapException ex)
                {
                    log.Warning(ex, "{Function} Error removing user {User} from group {Group}.", "RemoveUserFromGroup",
                        userName, groupName);
                }
            }
            else
            {
                log.Information("{Function} User {Username} exists = {UExists}, {Group} exists = {GExists}",
                    "RemoveUserFromGroup", userName, user != null, groupName, group != null);
            }
        }

        #endregion

        #region Shared

        private string GetBaseDn(IEnumerable<string> ouPath = null)
        {
            return GetOuFormat(ouPath) + domainNameDcFormat; // e.g. OU=Users,DC=example,DC=com
        }

        private static string GetOuFormat(IEnumerable<string> ouPath)
        {
            return ouPath != null ? ouPath.Aggregate("", (current, ou) => "OU=" + ou + "," + current.Trim()) : "";
        }

        #endregion
    }
}
