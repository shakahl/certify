using System;
using Certify.Core.Management.Access;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Certify.Models;
using Certify.Models.Providers;
using Serilog;

namespace Certify.Core.Tests.Unit
{

    public class MemoryObjectStore : IObjectStore
    {
        ConcurrentDictionary<string, object> _store = new ConcurrentDictionary<string, object>();

        public Task<T> Load<T>(string id)
        {
            if (_store.TryGetValue(id, out object value))
            {
                return Task.FromResult((T)value);
            }
            else
            {
                var empty = (T)Activator.CreateInstance(typeof(T));

                return Task.FromResult(empty);
            }
        }

        public Task<bool> Save<T>(string id, object item)
        {
            _ = _store.AddOrUpdate(id, item, (key, oldVal) => item);
            return Task.FromResult(true);
        }
    }

    [TestClass]
    public class AccessControlTests
    {

        private List<SecurityPrinciple> GetTestSecurityPrinciples()
        {

            return new List<SecurityPrinciple> {
                new SecurityPrinciple {
                    Id = "admin_01",
                    Username = "admin",
                    Description = "Administrator account",
                    Email="info@test.com", Password="ABCDEFG",
                    PrincipleType= SecurityPrincipleType.User,
                    SystemRoleIds=new List<string>{ StandardRoles.Administrator.Id
    }
},
                new SecurityPrinciple
                {
                    Id = "domain_owner_01",
                    Username = "demo_owner",
                    Description = "Example domain owner",
                    Email = "domains@test.com",
                    Password = "ABCDEFG",
                    PrincipleType = SecurityPrincipleType.User,
                    SystemRoleIds = new List<string> { StandardRoles.DomainOwner.Id }
                },
                 new SecurityPrinciple
                 {
                     Id = "devops_user_01",
                     Username = "devops_01",
                     Description = "Example devops user",
                     Email = "devops01@test.com",
                     Password = "ABCDEFG",
                     PrincipleType = SecurityPrincipleType.User,
                     SystemRoleIds = new List<string> { StandardRoles.CertificateConsumer.Id, StandardRoles.DomainRequestor.Id }
                 },
                  new SecurityPrinciple
                  {
                      Id = "devops_app_01",
                      Username = "devapp_01",
                      Description = "Example devops app domain consumer",
                      Email = "dev_app01@test.com",
                      Password = "ABCDEFG",
                      PrincipleType = SecurityPrincipleType.User,
                      SystemRoleIds = new List<string> { StandardRoles.CertificateConsumer.Id }
                  }
            };
        }

        public List<ResourceProfile> GetTestResourceProfiles()
        {
            return new List<ResourceProfile> {
                new ResourceProfile {
                    ResourceType=StandardResourceTypes.System,
                    AssignedRoles= new List<ResourceAssignedRole>{
                            new ResourceAssignedRole{ RoleId=StandardRoles.Administrator.Id, PrincipleId = "admin_01" },
                            new ResourceAssignedRole{ RoleId=StandardRoles.CertificateConsumer.Id, PrincipleId = "devops_user_01" },
                            new ResourceAssignedRole{ RoleId=StandardRoles.DomainRequestor.Id, PrincipleId = "devops_user_01" }
                    }
             }
            };
        }
        [TestMethod]
        public async Task TestAccessControlChecks()
        {
            var log = new LoggerConfiguration()
                   .WriteTo.Debug()
                   .CreateLogger();

            var loggy = new Loggy(log);

            var access = new AccessControl(loggy, new MemoryObjectStore());

            var contextUserId = "[test]";

            // add test security principles
            var allPrinciples = GetTestSecurityPrinciples();
            foreach (var p in allPrinciples)
            {
                _ = await access.AddSecurityPrinciple(p, contextUserId, bypassIntegrityCheck: true);
            }

            // assign resource roles per principle
            var allResourceProfiles = GetTestResourceProfiles();
            foreach (var r in allResourceProfiles)
            {
                _ = await access.AddResourceProfile(r, contextUserId, bypassIntegrityCheck: true);
            }

            var hasAccess = await access.IsPrincipleInRole("admin_01", StandardRoles.Administrator.Id, contextUserId);
            Assert.IsTrue(hasAccess);

            hasAccess = await access.IsPrincipleInRole("admin_02", StandardRoles.Administrator.Id, contextUserId);
            Assert.IsFalse(hasAccess);
        }
    }
}
