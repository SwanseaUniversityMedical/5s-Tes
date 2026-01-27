using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FiveSafesTes.Core.Models
{
    public class KeycloakCredentials: BaseModel
    {
        public int Id { get; set; }

        [DisplayName("Enter Keycloak username")]
        [Required(ErrorMessage = "Username is required.")]
        public string UserName { get; set; }
        [DisplayName("Enter Keycloak password")]
        [Required(ErrorMessage = "Password is required.")]
        public string PasswordEnc { get; set; }

        [NotMapped]
        [DisplayName("Confirm Keycloak password")]
        [Compare("PasswordEnc", ErrorMessage = "Confirm password doesn't match, Type again!")]
        public string ConfirmPassword { get; set; }

        public CredentialType CredentialType { get; set; }

        [NotMapped]
        
        public bool Valid { get; set; }
    }

    public enum CredentialType
    {
        Submission =0,
        Tre = 1,
        Egress = 2
    }

    
}
