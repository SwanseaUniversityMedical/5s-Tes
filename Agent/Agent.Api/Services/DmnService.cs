using System.Text;
using System.Xml;
using System.Xml.Linq;
using Credentials.Models.Services;
using FiveSafesTes.Core.Models;

namespace Agent.Api.Services
{
    public interface IDmnService
    {
        Task<DmnDecisionTable> LoadDmnTableAsync(string filePath);
        Task<bool> SaveDmnTableAsync(string filePath, DmnDecisionTable table);
        Task<DmnRule> AddRuleAsync(string filePath, CreateDmnRuleRequest request);
        Task<bool> UpdateRuleAsync(string filePath, UpdateDmnRuleRequest request);
        Task<bool> DeleteRuleAsync(string filePath, string ruleId);
        Task<bool> ValidateDmnAsync(string filePath);
        Task DeployDmnToZeebeAsync(string filePath);
    }

    public class DmnService : IDmnService
    {
        private readonly ILogger<DmnService> _logger;
        private readonly IServicedZeebeClient _zeebeClient;
        private const string DmnNamespace = "https://www.omg.org/spec/DMN/20191111/MODEL/";

        public DmnService(ILogger<DmnService> logger, IServicedZeebeClient zeebeClient)
        {
            _logger = logger;
            _zeebeClient = zeebeClient;
        }

        /// <summary>
        /// Loads and parses a DMN file into a structured object
        /// </summary>
        public async Task<DmnDecisionTable> LoadDmnTableAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"DMN file not found: {filePath}");
                }

                var xmlContent = await File.ReadAllTextAsync(filePath);
                var doc = XDocument.Parse(xmlContent);
                XNamespace ns = DmnNamespace;

                var decision = doc.Descendants(ns + "decision").FirstOrDefault();
                if (decision == null)
                {
                    throw new InvalidOperationException("No decision element found in DMN file");
                }

                var decisionTable = decision.Descendants(ns + "decisionTable").FirstOrDefault();
                if (decisionTable == null)
                {
                    throw new InvalidOperationException("No decisionTable element found in DMN file");
                }

                var result = new DmnDecisionTable
                {
                    DecisionId = decision.Attribute("id")?.Value,
                    DecisionName = decision.Attribute("name")?.Value,
                    HitPolicy = decisionTable.Attribute("hitPolicy")?.Value ?? "COLLECT"
                };

                // Parse inputs
                foreach (var input in decisionTable.Elements(ns + "input"))
                {
                    var inputExpression = input.Element(ns + "inputExpression");
                    result.Inputs.Add(new DmnInput
                    {
                        Id = input.Attribute("id")?.Value,
                        Label = input.Attribute("label")?.Value,
                        Expression = inputExpression?.Element(ns + "text")?.Value,
                        TypeRef = inputExpression?.Attribute("typeRef")?.Value ?? "string"
                    });
                }

                // Parse outputs
                foreach (var output in decisionTable.Elements(ns + "output"))
                {
                    result.Outputs.Add(new DmnOutput
                    {
                        Id = output.Attribute("id")?.Value,
                        Label = output.Attribute("label")?.Value,
                        Name = output.Attribute("name")?.Value,
                        TypeRef = output.Attribute("typeRef")?.Value ?? "string"
                    });
                }

                // Parse rules
                foreach (var rule in decisionTable.Elements(ns + "rule"))
                {
                    var dmnRule = new DmnRule
                    {
                        Id = rule.Attribute("id")?.Value,
                        Description = rule.Element(ns + "description")?.Value
                    };

                    // Parse input entries
                    foreach (var inputEntry in rule.Elements(ns + "inputEntry"))
                    {
                        dmnRule.InputEntries.Add(new DmnInputEntry
                        {
                            Id = inputEntry.Attribute("id")?.Value,
                            Text = inputEntry.Element(ns + "text")?.Value
                        });
                    }

                    // Parse output entries
                    foreach (var outputEntry in rule.Elements(ns + "outputEntry"))
                    {
                        dmnRule.OutputEntries.Add(new DmnOutputEntry
                        {
                            Id = outputEntry.Attribute("id")?.Value,
                            Text = outputEntry.Element(ns + "text")?.Value
                        });
                    }

                    result.Rules.Add(dmnRule);
                }

                _logger.LogInformation($"Successfully loaded DMN table with {result.Rules.Count} rules");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading DMN file: {filePath}");
                throw;
            }
        }

        /// <summary>
        /// Saves a DMN decision table back to XML file
        /// </summary>
        public async Task<bool> SaveDmnTableAsync(string filePath, DmnDecisionTable table)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"DMN file not found: {filePath}");
                }

                var xmlContent = await File.ReadAllTextAsync(filePath);
                var doc = XDocument.Parse(xmlContent);
                XNamespace ns = DmnNamespace;

                var decision = doc.Descendants(ns + "decision").FirstOrDefault();
                if (decision == null)
                {
                    throw new InvalidOperationException("No decision element found in DMN file");
                }

                var decisionTable = decision.Descendants(ns + "decisionTable").FirstOrDefault();
                if (decisionTable == null)
                {
                    throw new InvalidOperationException("No decisionTable element found");
                }

                // Update decision attributes
                decision.SetAttributeValue("name", table.DecisionName);
                decisionTable.SetAttributeValue("hitPolicy", table.HitPolicy);

                // Remove existing rules
                decisionTable.Elements(ns + "rule").Remove();

                // Add updated rules
                foreach (var rule in table.Rules)
                {
                    var ruleElement = new XElement(ns + "rule",
                        new XAttribute("id", rule.Id ?? GenerateId("Rule"))
                    );

                    if (!string.IsNullOrWhiteSpace(rule.Description))
                    {
                        ruleElement.Add(new XElement(ns + "description", rule.Description));
                    }

                    // Add input entries
                    foreach (var inputEntry in rule.InputEntries)
                    {
                        ruleElement.Add(new XElement(ns + "inputEntry",
                            new XAttribute("id", inputEntry.Id ?? GenerateId("InputEntry")),
                            new XElement(ns + "text", inputEntry.Text ?? "")
                        ));
                    }

                    // Add output entries
                    foreach (var outputEntry in rule.OutputEntries)
                    {
                        ruleElement.Add(new XElement(ns + "outputEntry",
                            new XAttribute("id", outputEntry.Id ?? GenerateId("OutputEntry")),
                            new XElement(ns + "text", outputEntry.Text ?? "")
                        ));
                    }

                    decisionTable.Add(ruleElement);
                }

                // Save with proper formatting
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    Encoding = Encoding.UTF8,
                    OmitXmlDeclaration = false
                };

                using (var writer = XmlWriter.Create(filePath, settings))
                {
                    doc.Save(writer);
                }

                _logger.LogInformation($"Successfully saved DMN table with {table.Rules.Count} rules");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving DMN file: {filePath}");
                throw;
            }
        }

        /// <summary>
        /// Adds a new rule to the DMN table
        /// </summary>
        public async Task<DmnRule> AddRuleAsync(string filePath, CreateDmnRuleRequest request)
        {
            try
            {
                var table = await LoadDmnTableAsync(filePath);

                // Validate input/output counts
                if (request.InputValues.Count != table.Inputs.Count)
                {
                    throw new ArgumentException($"Expected {table.Inputs.Count} input values, but got {request.InputValues.Count}");
                }

                if (request.OutputValues.Count != table.Outputs.Count)
                {
                    throw new ArgumentException($"Expected {table.Outputs.Count} output values, but got {request.OutputValues.Count}");
                }

                var newRule = new DmnRule
                {
                    Id = GenerateId("DecisionRule"),
                    Description = request.Description
                };

                // Create input entries
                for (int i = 0; i < request.InputValues.Count; i++)
                {
                    newRule.InputEntries.Add(new DmnInputEntry
                    {
                        Id = GenerateId("InputEntry"),
                        Text = request.InputValues[i]
                    });
                }

                // Create output entries
                for (int i = 0; i < request.OutputValues.Count; i++)
                {
                    newRule.OutputEntries.Add(new DmnOutputEntry
                    {
                        Id = GenerateId("OutputEntry"),
                        Text = request.OutputValues[i]
                    });
                }

                table.Rules.Add(newRule);
                await SaveDmnTableAsync(filePath, table);

                _logger.LogInformation($"Added new rule {newRule.Id} to DMN table");
                return newRule;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding rule to DMN table");
                throw;
            }
        }

        /// <summary>
        /// Updates an existing rule in the DMN table
        /// </summary>
        public async Task<bool> UpdateRuleAsync(string filePath, UpdateDmnRuleRequest request)
        {
            try
            {
                var table = await LoadDmnTableAsync(filePath);

                var rule = table.Rules.FirstOrDefault(r => r.Id == request.RuleId);
                if (rule == null)
                {
                    throw new ArgumentException($"Rule with ID {request.RuleId} not found");
                }

                // Validate input/output counts
                if (request.InputValues.Count != table.Inputs.Count)
                {
                    throw new ArgumentException($"Expected {table.Inputs.Count} input values, but got {request.InputValues.Count}");
                }

                if (request.OutputValues.Count != table.Outputs.Count)
                {
                    throw new ArgumentException($"Expected {table.Outputs.Count} output values, but got {request.OutputValues.Count}");
                }

                // Update description
                rule.Description = request.Description;

                // Update input entries
                for (int i = 0; i < request.InputValues.Count; i++)
                {
                    if (i < rule.InputEntries.Count)
                    {
                        rule.InputEntries[i].Text = request.InputValues[i];
                    }
                }

                // Update output entries
                for (int i = 0; i < request.OutputValues.Count; i++)
                {
                    if (i < rule.OutputEntries.Count)
                    {
                        rule.OutputEntries[i].Text = request.OutputValues[i];
                    }
                }

                await SaveDmnTableAsync(filePath, table);

                _logger.LogInformation($"Updated rule {request.RuleId} in DMN table");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating rule {request.RuleId}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a rule from the DMN table
        /// </summary>
        public async Task<bool> DeleteRuleAsync(string filePath, string ruleId)
        {
            try
            {
                var table = await LoadDmnTableAsync(filePath);

                var rule = table.Rules.FirstOrDefault(r => r.Id == ruleId);
                if (rule == null)
                {
                    throw new ArgumentException($"Rule with ID {ruleId} not found");
                }

                table.Rules.Remove(rule);
                await SaveDmnTableAsync(filePath, table);

                _logger.LogInformation($"Deleted rule {ruleId} from DMN table");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting rule {ruleId}");
                throw;
            }
        }

        /// <summary>
        /// Validates DMN file structure
        /// </summary>
        public async Task<bool> ValidateDmnAsync(string filePath)
        {
            try
            {
                var table = await LoadDmnTableAsync(filePath);

                // Basic validation
                if (string.IsNullOrWhiteSpace(table.DecisionId))
                {
                    throw new InvalidOperationException("Decision ID is required");
                }

                if (table.Inputs.Count == 0)
                {
                    throw new InvalidOperationException("At least one input is required");
                }

                if (table.Outputs.Count == 0)
                {
                    throw new InvalidOperationException("At least one output is required");
                }

                // Validate each rule
                foreach (var rule in table.Rules)
                {
                    if (rule.InputEntries.Count != table.Inputs.Count)
                    {
                        throw new InvalidOperationException($"Rule {rule.Id} has incorrect number of input entries");
                    }

                    if (rule.OutputEntries.Count != table.Outputs.Count)
                    {
                        throw new InvalidOperationException($"Rule {rule.Id} has incorrect number of output entries");
                    }
                }

                _logger.LogInformation($"DMN validation successful: {table.Rules.Count} rules validated");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"DMN validation failed for {filePath}");
                throw;
            }
        }

        /// <summary>
        /// Deploys the DMN file to Zeebe
        /// </summary>
        public async Task DeployDmnToZeebeAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"DMN file not found: {filePath}");
                }

                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var fileName = Path.GetFileName(filePath);
                    await _zeebeClient.DeployModel(stream, fileName);
                }

                _logger.LogInformation($"DMN deployed to Zeebe successfully: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to deploy DMN to Zeebe: {filePath}");
                throw new InvalidOperationException("Failed to deploy DMN to Zeebe: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Generates a unique ID for DMN elements
        /// </summary>
        private string GenerateId(string prefix)
        {
            return $"{prefix}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }
    }
}
