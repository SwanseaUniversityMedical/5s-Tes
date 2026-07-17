using System.DirectoryServices.Protocols;
using System.Security.Principal;

namespace TeleportAD.Models
{
    public class LdapUser
    {
        public string DistinguishedName { get; set; }
        public string SamAccountName { get; set; }
        public string UserPrincipalName { get; set; }
        public string DisplayName { get; set; }
        public SearchResultEntry RawEntry { get; set; }
        public string EmailAddress { get; set; }
        public string Description { get; set; }
        public string GivenName { get; set; }
        public string Surname { get; set; }

        public SecurityIdentifier Sid { get; set; }

        public int? UserAccountCtrl { get; set; }
        public string Name { get; set; }
        public string TelephoneNumber { get; set; }
        public List<string> MemberOf { get; set; }


        public string StringSid { get; set; }




        public LdapUser(string username, string sid)
        {
            SamAccountName = username;
            StringSid = sid;
        }
        public LdapUser(SearchResultEntry entry)
        {
            var attrs = entry.Attributes;
            DistinguishedName = entry.DistinguishedName;
            SamAccountName = GetSearchResultAttributeSafe(attrs, "sAMAccountName");
            UserPrincipalName = GetSearchResultAttributeSafe(attrs, "userPrincipalName");
            DisplayName = GetSearchResultAttributeSafe(attrs, "displayName");
            EmailAddress = GetSearchResultAttributeSafe(attrs, "mail");
            GivenName = GetSearchResultAttributeSafe(attrs, "givenName");
            Surname = GetSearchResultAttributeSafe(attrs, "sn");
            Description = GetSearchResultAttributeSafe(attrs, "description");
            Name = GetSearchResultAttributeSafe(attrs, "name");
            TelephoneNumber = GetSearchResultAttributeSafe(attrs, "telephoneNumber");
            MemberOf = GetearchResultAttributeArraySafe(attrs, "memberOf");
            Sid = GetSearchResultSidSafe(attrs, "objectSid");

            var userAccountControlAttr = GetSearchResultAttributeSafe(attrs, "userAccountControl");
            if (!string.IsNullOrWhiteSpace(userAccountControlAttr))
            {
                UserAccountCtrl = int.Parse(userAccountControlAttr);
            }

            RawEntry = entry;


        }



        private static string GetSearchResultAttributeSafe(SearchResultAttributeCollection attrs, string name)
        {
            return attrs.Contains(name) ? attrs[name][0]?.ToString() : "";
        }

        private static SecurityIdentifier GetSearchResultSidSafe(SearchResultAttributeCollection attrs, string name)
        {
            if (!attrs.Contains(name) || attrs[name].Count == 0)
                return null;

            var sidBytes = (byte[])attrs[name][0];
            return new SecurityIdentifier(sidBytes, 0);
        }

        private static List<string> GetearchResultAttributeArraySafe(SearchResultAttributeCollection attrs, string name)
        {


            var memberOfList = new List<string>();

            if (!attrs.Contains(name)) return memberOfList;
            // GetValues(typeof(string)) returns an object[] of all group DNs
            var values = attrs[name].GetValues(typeof(string));

            // Cast each object to string and convert to List<string>
            memberOfList = values.Cast<string>().ToList();

            return memberOfList;
        }


    }
}