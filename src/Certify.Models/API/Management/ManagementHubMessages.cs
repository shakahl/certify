﻿using System;
using System.Collections.Generic;

namespace Certify.API.Management
{
    public class ManagementHubMessages
    {
        public const string SendCommandRequest = "SendCommandRequest";
        public const string ReceiveCommandResult = "ReceiveCommandResult";
        public const string GetCommandResult = "GetCommandResult";
    }

    public class ManagementHubCommands
    {
        public const string GetInstanceInfo = "GetInstanceInfo";
        public const string GetInstanceManagedItems = "GetInstanceManagedItems";
        public const string GetInstanceManagedItem = "GetInstanceManagedItem";
        public const string UpdateInstanceManagedItem = "UpdateInstanceManagedItem";
    }

    /// <summary>
    /// A command that can be sent asynchronously to an instance (each instance is a hub client)
    /// </summary>
    public class InstanceCommandRequest
    {
        public InstanceCommandRequest()
        {

        }

        public InstanceCommandRequest(string commandType)
        {
            CommandId = Guid.NewGuid();
            CommandType = commandType;
        }
        public InstanceCommandRequest(string commandType, KeyValuePair<string, string>[] values) : this(commandType)
        {
            Value = System.Text.Json.JsonSerializer.Serialize(values);
        }
        /// <summary>
        /// Unique ID of this command
        /// </summary>
        public Guid CommandId { get; set; }

        /// <summary>
        /// Command type
        /// </summary>
        public string CommandType { get; set; } = string.Empty;

        /// <summary>
        /// Command associated value
        /// </summary>
        public string? Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// A result (eventually) received from an instance command
    /// </summary>
    public class InstanceCommandResult
    {
        /// <summary>
        /// Guid of the original command being responded to
        /// </summary>
        public Guid CommandId { get; set; }

        public string? InstanceId { get; set; }
        public DateTimeOffset? Received { get; set; }
        /// <summary>
        /// Response value
        /// </summary>
        public string? Value { get; set; }

        public object? ObjectValue { get; set; }
    }

    /// <summary>
    /// General ad-hoc message sent from an instance to the management hub such as a progress report or new/updated managed item
    /// </summary>
    public class InstanceMessage
    {
        /// <summary>
        /// Type of message instance is sending
        /// </summary>
        public string MessageType { get; set; } = string.Empty;

        /// <summary>
        /// Value of message instance is sending, to be interpreted by the management hub
        /// </summary>
        public string? Value { get; set; }
    }
}