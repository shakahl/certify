﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Certify.Client;
using Certify.Models.Hub;
using Certify.Models;
using Certify.Models.Config;
using Certify.Shared.Core.Utils;
using System.Runtime.InteropServices;
using Certify.Locales;

namespace Certify.Management
{
    public partial class CertifyManager
    {
        private ManagementServerClient _managementServerClient;
        private string _managementServerConnectionId = string.Empty;

        private async Task EnsureMgmtHubConnection()
        {
            // connect/reconnect to management hub if enabled
            if (_managementServerClient == null || !_managementServerClient.IsConnected())
            {
                var mgmtHubUri = Environment.GetEnvironmentVariable("CERTIFY_MANAGEMENT_HUB") ?? _serverConfig.ManagementServerHubUri;

                if (!string.IsNullOrWhiteSpace(mgmtHubUri))
                {
                    await StartManagementHubConnection(mgmtHubUri);
                }
            }
            else
            {

                // send heartbeat message to management hub
                SendHeartbeatToManagementHub();
            }
        }

        private void SendHeartbeatToManagementHub()
        {
            _managementServerClient.SendInstanceInfo(Guid.NewGuid(), false);
        }

        private async Task StartManagementHubConnection(string hubUri)
        {

            _serviceLog.Debug("Attempting connection to management hub {hubUri}", hubUri);

            var appVersion = Util.GetAppVersion().ToString();

            var instanceInfo = new ManagedInstanceInfo
            {
                InstanceId = $"{this.InstanceId}",
                Title = $"{Environment.MachineName}",
                OS = EnvironmentUtil.GetFriendlyOSName(detailed: false),
                OSVersion = EnvironmentUtil.GetFriendlyOSName(),
                ClientVersion = appVersion,
                ClientName = ConfigResources.AppName
            };

            if (_managementServerClient != null)
            {
                _managementServerClient.OnGetCommandResult -= _managementServerClient_OnGetCommandResult;
                _managementServerClient.OnConnectionReconnecting -= _managementServerClient_OnConnectionReconnecting;
            }

            _managementServerClient = new ManagementServerClient(hubUri, instanceInfo);

            try
            {
                await _managementServerClient.ConnectAsync();

                _managementServerClient.OnGetCommandResult += _managementServerClient_OnGetCommandResult;
                _managementServerClient.OnConnectionReconnecting += _managementServerClient_OnConnectionReconnecting;
            }
            catch (Exception ex)
            {
                _serviceLog.Error(ex, "Failed to create connection to management hub {hubUri}", hubUri);

                _managementServerClient = null;
            }
        }

        private async Task<InstanceCommandResult> _managementServerClient_OnGetCommandResult(InstanceCommandRequest arg)
        {
            object val = null;

            if (arg.CommandType == ManagementHubCommands.GetManagedItem)
            {
                // Get a single managed item by id
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var managedCertIdArg = args.FirstOrDefault(a => a.Key == "managedCertId");
                val = await GetManagedCertificate(managedCertIdArg.Value);
            }
            else if (arg.CommandType == ManagementHubCommands.GetManagedItems)
            {
                // Get all managed items
                var items = await GetManagedCertificates(new ManagedCertificateFilter { });
                val = new ManagedInstanceItems { InstanceId = InstanceId, Items = items };
            }
            else if (arg.CommandType == ManagementHubCommands.GetStatusSummary)
            {
                var s = await GetManagedCertificateSummary(new ManagedCertificateFilter { });
                s.InstanceId = InstanceId;
                val = s;
            }
            else if (arg.CommandType == ManagementHubCommands.GetManagedItemLog)
            {
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var managedCertIdArg = args.FirstOrDefault(a => a.Key == "managedCertId");
                var limit = args.FirstOrDefault(a => a.Key == "limit");

                val = await GetItemLog(managedCertIdArg.Value, int.Parse(limit.Value));
            }
            else if (arg.CommandType == ManagementHubCommands.GetManagedItemRenewalPreview)
            {
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var managedCertArg = args.FirstOrDefault(a => a.Key == "managedCert");
                var managedCert = JsonSerializer.Deserialize<ManagedCertificate>(managedCertArg.Value);

                val = await GeneratePreview(managedCert);
            }
            else if (arg.CommandType == ManagementHubCommands.UpdateManagedItem)
            {
                // update a single managed item 
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var managedCertArg = args.FirstOrDefault(a => a.Key == "managedCert");
                var managedCert = JsonSerializer.Deserialize<ManagedCertificate>(managedCertArg.Value);

                val = await UpdateManagedCertificate(managedCert);
            }
            else if (arg.CommandType == ManagementHubCommands.RemoveDeleteManagedItem)
            {
                // delete a single managed item 
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var managedCertIdArg = args.FirstOrDefault(a => a.Key == "managedCertId");

                await DeleteManagedCertificate(managedCertIdArg.Value);
            }
            else if (arg.CommandType == ManagementHubCommands.TestManagedItemConfiguration)
            {
                // test challenge response config for a single managed item 
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var managedCertArg = args.FirstOrDefault(a => a.Key == "managedCert");
                var managedCert = JsonSerializer.Deserialize<ManagedCertificate>(managedCertArg.Value);

                var log = ManagedCertificateLog.GetLogger(managedCert.Id, _loggingLevelSwitch);

                val = await TestChallenge(log, managedCert, isPreviewMode: true);

            }
            else if (arg.CommandType == ManagementHubCommands.PerformManagedItemRequest)
            {
                // attempt certificate order
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var managedCertIdArg = args.FirstOrDefault(a => a.Key == "managedCertId");
                var managedCert = await GetManagedCertificate(managedCertIdArg.Value);

                var progressState = new RequestProgressState(RequestState.Running, "Starting..", managedCert);
                var progressIndicator = new Progress<RequestProgressState>(progressState.ProgressReport);

                _ = await PerformCertificateRequest(
                                                        null,
                                                        managedCert,
                                                        progressIndicator,
                                                        resumePaused: true,
                                                        isInteractive: true
                                                        );

                val = true;
            }
            else if (arg.CommandType == ManagementHubCommands.GetCertificateAuthorities)
            {
                val = await GetCertificateAuthorities();
            }
            else if (arg.CommandType == ManagementHubCommands.UpdateCertificateAuthority)
            {
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var itemArg = args.FirstOrDefault(a => a.Key == "certificateAuthority");
                var item = JsonSerializer.Deserialize<CertificateAuthority>(itemArg.Value);

                val = await UpdateCertificateAuthority(item);
            }
            else if (arg.CommandType == ManagementHubCommands.RemoveCertificateAuthority)
            {
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var itemArg = args.FirstOrDefault(a => a.Key == "id");
                val = await RemoveCertificateAuthority(itemArg.Value);
            }
            else if (arg.CommandType == ManagementHubCommands.GetAcmeAccounts)
            {
                val = await GetAccountRegistrations();
            }
            else if (arg.CommandType == ManagementHubCommands.AddAcmeAccount)
            {
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var registrationArg = args.FirstOrDefault(a => a.Key == "registration");
                var registration = JsonSerializer.Deserialize<ContactRegistration>(registrationArg.Value);

                val = await AddAccount(registration);
            }
            else if (arg.CommandType == ManagementHubCommands.RemoveAcmeAccount)
            {
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var itemArg = args.FirstOrDefault(a => a.Key == "storageKey");
                var deactivateArg = args.FirstOrDefault(a => a.Key == "deactivate");
                val = await RemoveAccount(itemArg.Value, bool.Parse(deactivateArg.Value));
            }
            else if (arg.CommandType == ManagementHubCommands.GetStoredCredentials)
            {
                val = await _credentialsManager.GetCredentials();
            }
            else if (arg.CommandType == ManagementHubCommands.UpdateStoredCredential)
            {
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var itemArg = args.FirstOrDefault(a => a.Key == "item");
                var storedCredential = JsonSerializer.Deserialize<StoredCredential>(itemArg.Value);

                val = await _credentialsManager.Update(storedCredential);
            }
            else if (arg.CommandType == ManagementHubCommands.RemoveStoredCredential)
            {
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var itemArg = args.FirstOrDefault(a => a.Key == "storageKey");
                val = await _credentialsManager.Delete(_itemManager, itemArg.Value);
            }
            else if (arg.CommandType == ManagementHubCommands.GetChallengeProviders)
            {
                val = await Core.Management.Challenges.ChallengeProviders.GetChallengeAPIProviders();
            }

            else if (arg.CommandType == ManagementHubCommands.GetDnsZones)
            {
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);
                var providerTypeArg = args.FirstOrDefault(a => a.Key == "providerTypeId");
                var credentialIdArg = args.FirstOrDefault(a => a.Key == "credentialId");

                val = await GetDnsProviderZones(providerTypeArg.Value, credentialIdArg.Value);
            }
            else if (arg.CommandType == ManagementHubCommands.GetDeploymentProviders)
            {
                val = await GetDeploymentProviders();
            }
            else if (arg.CommandType == ManagementHubCommands.ExecuteDeploymentTask)
            {
                var args = JsonSerializer.Deserialize<KeyValuePair<string, string>[]>(arg.Value);

                var managedCertificateIdArg = args.FirstOrDefault(a => a.Key == "managedCertificateId");
                var taskIdArg = args.FirstOrDefault(a => a.Key == "taskId");

                val = await PerformDeploymentTask(null, managedCertificateIdArg.Value, taskIdArg.Value, isPreviewOnly: false, skipDeferredTasks: false, forceTaskExecution: false);
            }
            else if (arg.CommandType == ManagementHubCommands.Reconnect)
            {
                await _managementServerClient.Disconnect();
            }

            var result = new InstanceCommandResult { CommandId = arg.CommandId, Value = JsonSerializer.Serialize(val) };

            result.ObjectValue = val;

            return result;
        }

        private void _managementServerClient_OnConnectionReconnecting()
        {
            _serviceLog.Warning("Reconnecting to Management.");
        }

        private void GenerateDemoItems()
        {
            var items = DemoDataGenerator.GenerateDemoItems();
            foreach (var item in items)
            {
                _ = UpdateManagedCertificate(item);
            }
        }
    }
}
