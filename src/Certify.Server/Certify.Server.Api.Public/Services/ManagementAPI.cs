﻿using System.Text.Json;
using Certify.API.Management;
using Certify.Client;
using Certify.Models;
using Certify.Server.Api.Public.SignalR.ManagementHub;
using Microsoft.AspNetCore.SignalR;

namespace Certify.Server.Api.Public.Services
{
    public class ManagementAPI
    {
        IInstanceManagementStateProvider _mgmtStateProvider;
        IHubContext<InstanceManagementHub, IInstanceManagementHub> _mgmtHubContext;
        ICertifyInternalApiClient _backendAPIClient;

        public ManagementAPI(IInstanceManagementStateProvider mgmtStateProvider, IHubContext<InstanceManagementHub, IInstanceManagementHub> mgmtHubContext, ICertifyInternalApiClient backendAPIClient)
        {
            _mgmtStateProvider = mgmtStateProvider;
            _mgmtHubContext = mgmtHubContext;
            _backendAPIClient = backendAPIClient;
        }

        /// <summary>
        /// Fetch managed cert details from the target instance
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="managedCertId"></param>
        /// <param name="authContext"></param>
        /// <returns></returns>
        public async Task<ManagedCertificate?> GetManagedCertificate(string instanceId, string managedCertId, AuthContext authContext)
        {
            // get managed cert via local api or via management hub

            var args = new KeyValuePair
                <string, string>[] {
                    new("instanceId", instanceId) ,
                    new("managedCertId", managedCertId)
                };

            var cmd = new InstanceCommandRequest(ManagementHubCommands.GetInstanceManagedItem, args);
            var result = await GetCommandResult(instanceId, cmd);

            if (result?.Value != null)
            {
                return JsonSerializer.Deserialize<ManagedCertificate>(result.Value);

            }
            else
            {
                return null;
            }
        }

        public async Task<ManagedCertificate?> UpdateManagedCertificate(string instanceId, ManagedCertificate managedCert, AuthContext authContext)
        {
            // get managed cert via local api or via management hub

            var args = new KeyValuePair<string, string>[] {
                    new("instanceId", instanceId) ,
                    new("managedCert", JsonSerializer.Serialize(managedCert))
                };

            var cmd = new InstanceCommandRequest(ManagementHubCommands.UpdateInstanceManagedItem, args);

            var result = await GetCommandResult(instanceId, cmd);

            if (result?.Value != null)
            {
                var update =  JsonSerializer.Deserialize<ManagedCertificate>(result.Value);
                
                _mgmtStateProvider.UpdateCachedManagedInstanceItem(instanceId, update);
                return update;
            }
            else
            {
                return null;
            }
        }

        private async Task<InstanceCommandResult?> GetCommandResult(string instanceId, InstanceCommandRequest cmd)
        {
            var connectionId = _mgmtStateProvider.GetConnectionIdForInstance(instanceId);

            if (connectionId == null)
            {
                throw new Exception("Instance connection info not known, cannot send commands to instance.");
            }

            _mgmtStateProvider.AddAwaitedCommandRequest(cmd);

            await _mgmtHubContext.Clients.Client(connectionId).SendCommandRequest(cmd);

            return await _mgmtStateProvider.ConsumeAwaitedCommandResult(cmd.CommandId);
        }
    }
}