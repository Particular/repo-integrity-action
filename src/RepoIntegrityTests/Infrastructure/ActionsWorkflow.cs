namespace RepoIntegrityTests.Infrastructure
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.Json.Serialization;
    using YamlDotNet.Serialization;

    public class ActionsWorkflow
    {
        JsonNode root;

        public ActionsWorkflow(string path)
        {
            var deserializer = new Deserializer();
            var yamlObject = deserializer.Deserialize(File.ReadAllText(path));

            var json = JsonSerializer.Serialize(yamlObject, new JsonSerializerOptions { WriteIndented = true });
            root = JsonSerializer.Deserialize<JsonNode>(json);

            var options = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            Name = root["name"]?.GetValue<string>();
            RunName = root["run-name"]?.GetValue<string>();
            On = ParseTriggerEvents(root["on"]);
            Permissions = JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(root["permissions"]);
            Env = JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(root["env"]);

            if (root["defaults"] is not null)
            {
                Defaults = JsonSerializer.Deserialize<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>(root["defaults"]);
            }

            Jobs = root["jobs"].AsObject().Select(pair =>
            {
                var job = JsonSerializer.Deserialize<WorkflowJob>(pair.Value, options);
                job.Id = pair.Key;

                if (pair.Value["defaults"] is not null)
                {
                    job.Defaults = JsonSerializer.Deserialize<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>(pair.Value["defaults"]);
                }

                return job;
            })
            .ToArray();
        }

        public string Name { get; }
        public string RunName { get; }
        public WorkflowTrigger[] On { get; }
        public IReadOnlyDictionary<string, string> Permissions { get; } = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> Env { get; } = new Dictionary<string, string>();

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Defaults { get; } = new Dictionary<string, IReadOnlyDictionary<string, string>>();

        public WorkflowJob[] Jobs { get; }

        static WorkflowTrigger[] ParseTriggerEvents(JsonNode on)
        {
            if (on is null)
            {
                return [];
            }

            var kind = on.GetValueKind();
            if (kind == JsonValueKind.String)
            {
                var stringValue = on.GetValue<string>();
                return [new WorkflowTrigger(stringValue)];
            }
            else if (kind == JsonValueKind.Array)
            {
                return on.AsArray().Select(evt => new WorkflowTrigger(evt.GetValue<string>())).ToArray();
            }
            else if (kind == JsonValueKind.Object)
            {
                return on.AsObject().Select(pair =>
                {
                    var trigger = new WorkflowTrigger(pair.Key);
                    if (pair.Value is not null)
                    {
                        if (trigger.EventId == "schedule")
                        {
                            trigger.Filters = new Dictionary<string, string[]>()
                            {
                                ["cron"] = (pair.Value as JsonArray)
                                    .OfType<JsonObject>()
                                    .Select(o => o["cron"].GetValue<string>())
                                    .ToArray()
                            };
                        }
                        else if (trigger.EventId == "workflow_dispatch")
                        {
                            // What to do about inputs inputs, like in https://github.com/Particular/ServiceControl/blob/master/.github/workflows/push-container-images.yml
                        }
                        else if (pair.Value is JsonObject asObj)
                        {
                            Dictionary<string, string[]> filters = [];
                            foreach (var filterPair in asObj)
                            {
                                if (filterPair.Value is JsonArray valueArray)
                                {
                                    var stringValues = JsonSerializer.Deserialize<string[]>(valueArray);
                                    filters.Add(filterPair.Key, stringValues);
                                }
                                else if (filterPair.Value is JsonValue jsonValue)
                                {
                                    filters.Add(filterPair.Key, [jsonValue.GetValue<string>()]);
                                }
                            }
                            trigger.Filters = filters;
                        }

                    }
                    return trigger;
                })
                .ToArray();
            }

            throw new System.Exception("Unable to parse workflow triggers");
        }
    }

    public class WorkflowTrigger(string eventId)
    {
        public string EventId { get; } = eventId;
        public IReadOnlyDictionary<string, string[]> Filters { get; set; } = new Dictionary<string, string[]>();

        public override string ToString() => $"Trigger on: {EventId}" + (Filters.Count > 0 ? $", filter on {string.Join(",", Filters.Keys)}" : "");
    }

    public class WorkflowJob
    {
        public string Id { get; set; }
        public string Uses { get; set; }
        public string Name { get; set; }
        [JsonPropertyName("runs-on")]
        public string RunsOn { get; set; }
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Defaults { get; set; } = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        public JsonObject Strategy { get; set; }
        public JobStep[] Steps { get; set; } = [];
    }

    public class JobStep
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string If { get; set; }
        public string Shell { get; set; }
        public IReadOnlyDictionary<string, string> Env { get; set; }
        public string Run { get; set; }
        public string Uses { get; set; }
        public IReadOnlyDictionary<string, string> With { get; set; }
    }

    public class DefaultDefinition
    {
        public string DefaultsFor { get; set; }
        public IReadOnlyDictionary<string, string> Settings { get; set; }
    }
}