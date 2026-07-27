using System.DirectoryServices.Protocols;

namespace TeleportUserManagement.Models
{
    public class LdapUser
    {
        public string DistinguishedName { get; set; }
        public string SamAccountName { get; set; }
        public string UserPrincipalName { get; set; }
        public string DisplayName { get; set; }
        public string EmailAddress { get; set; }
        public string Description { get; set; }
        public string GivenName { get; set; }
        public string Surname { get; set; }


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
        }

        private static string GetSearchResultAttributeSafe(SearchResultAttributeCollection attrs, string name)
        {
            return attrs.Contains(name) ? attrs[name][0]?.ToString() : "";
        }
    }
}
