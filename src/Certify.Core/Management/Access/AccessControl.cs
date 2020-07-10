using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Certify.Core.Management.Access
{
    public enum SecurityPrincipleType
    {
        User = 1,
        Application = 2
    }

    public class SecurityPrinciple
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// If true, user is a mapping to an external AD/LDAP group or user
        /// </summary>
        public bool IsDirectoryMapping { get; set; }

        public List<string> SystemRoleIds { get; set; }

        public SecurityPrincipleType PrincipleType { get; set; }
    }

    public class StandardRoles
    {
        public static Role Administrator { get; } = new Role("sysadmin", "Administrator", "Certify Server Administrator");
        public static Role DomainOwner { get; } = new Role("domain_owner", "Domain Owner", "Controls certificate access for a given domain");
        public static Role DomainRequestor { get; } = new Role("domain_requestor", "Domain Requestor", "Can request domains");
        public static Role CertificateConsumer { get; } = new Role("cert_consumer", "Certificate Consumer", "User of a given certificate");
    }

    public class Role
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }

        public Role() { }
        public Role(string id, string title, string description)
        {
            Id = id;
            Title = title;
            Description = description;
        }
    }

    public class DomainUserRole
    {
        public string DomainId { get; set; }
        public string UserId { get; set; }
        public string RoleId { get; set; }

    }
    /// <summary>
    /// Define a domain and who the controlling users are
    /// </summary>
    public class DomainProfile
    {
        public string DomainName { get; set; }
        public List<string> OwnerIds { get; set; }
        public List<Certify.Models.CertRequestChallengeConfig> DefaultChallenges { get; set; }
    }

    public class AccessControl
    {
        public async Task<List<SecurityPrinciple>> GetSecurityPrinciples()
        {
            return new List<SecurityPrinciple> {
                new SecurityPrinciple {
                    Id = "admin_01",
                    Username = "admin",
                    Description = "Administrator account",
                    Email="info@test.com", Password="ABCDEFG",
                    PrincipleType= SecurityPrincipleType.User,
                    SystemRoleIds=new List<string>{ StandardRoles.Administrator.Id }
                },
                new SecurityPrinciple {
                    Id = "domain_owner_01",
                    Username = "demo_owner",
                    Description = "Example domain owner",
                    Email="domains@test.com", Password="ABCDEFG",
                    PrincipleType= SecurityPrincipleType.User,
                    SystemRoleIds=new List<string>{ StandardRoles.DomainOwner.Id }
                },
                 new SecurityPrinciple {
                    Id = "devops_user_01",
                    Username = "devops_01",
                    Description = "Example devops user",
                    Email="devops01@test.com", Password="ABCDEFG",
                    PrincipleType= SecurityPrincipleType.User,
                    SystemRoleIds=new List<string>{ StandardRoles.CertificateConsumer.Id, StandardRoles.DomainRequestor.Id }
                },
                  new SecurityPrinciple {
                    Id = "devops_app_01",
                    Username = "devapp_01",
                    Description = "Example devops app domain consumer",
                    Email="dev_app01@test.com", Password="ABCDEFG",
                    PrincipleType= SecurityPrincipleType.User,
                    SystemRoleIds=new List<string>{ StandardRoles.CertificateConsumer.Id }
                }
            };
        }

        public async Task AddSecurityPrinciple(SecurityPrinciple user, string contextUserId)
        {

        }

        public async Task UpdateSecurityPrinciple(SecurityPrinciple user, string contextUserId)
        {

        }

        public async Task UpdateSecurityPrincipleRoles(string userId, List<string> roles, string contextUserId)
        {

        }

        public async Task DeleteSecurityPrinciple(string userId, string contextUserId)
        {

        }

        public async Task<List<Role>> GetRoles()
        {
            return new List<Role>
            {
                StandardRoles.Administrator,
                StandardRoles.DomainOwner,
                StandardRoles.CertificateConsumer
            };
        }

        public async Task<List<DomainProfile>> GetDomains(string contextUserId)
        {
            return new List<DomainProfile>
            {
                new DomainProfile{ DomainName="projectbids.co.uk" },
                new DomainProfile{ DomainName="dev.projectbids.co.uk" }
            };
        }
    }
}
