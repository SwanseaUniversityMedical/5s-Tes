using Novell.Directory.Ldap;

namespace TeleportUserManagement.Models
{
    public class LdapGroup
    {
        public string DistinguishedName { get; }
        public string CommonName { get; }
        public string Description { get; }

        public LdapEntry RawEntry { get; set; }
        public List<string> Members { get; }

        public Dictionary<string, string[]> Properties { get; } = new();

        public LdapGroup(LdapEntry entry)
        {
            DistinguishedName = entry.Dn;
            var atts = entry.GetAttributeSet();
            CommonName = GetAttributeSafe(atts, "cn");
            Description = GetAttributeSafe(atts, "description");
            Members = GetAttributeArraySafe(atts, "member");
            RawEntry = entry;
            foreach (var attr in atts)
            {
                Properties[attr.Name] = attr.StringValueArray;
            }
        }

        private static string GetAttributeSafe(LdapAttributeSet attrs, string name)
        {
            return attrs.TryGetValue(name, out var attr) ? attr?.StringValue : null;
        }

        private static List<string> GetAttributeArraySafe(LdapAttributeSet attrs, string name)
        {
            return attrs.TryGetValue(name, out var attr) ? attr?.StringValueArray?.ToList() : [];
        }
    }
}