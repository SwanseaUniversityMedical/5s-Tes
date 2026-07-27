using Novell.Directory.Ldap;

namespace TeleportUserManagement.Models
{
    public class LdapGroup
    {
        public string DistinguishedName { get; }

        public LdapGroup(LdapEntry entry)
        {
            DistinguishedName = entry.Dn;
        }
    }
}
