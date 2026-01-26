using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FiveSafesTes.Core.Models
{
    /// <summary>
    /// Represents a complete DMN decision table
    /// </summary>
    public class DmnDecisionTable
    {
        public string DecisionId { get; set; }
        public string DecisionName { get; set; }
        public string HitPolicy { get; set; } = "COLLECT";
        public List<DmnInput> Inputs { get; set; } = new List<DmnInput>();
        public List<DmnOutput> Outputs { get; set; } = new List<DmnOutput>();
        public List<DmnRule> Rules { get; set; } = new List<DmnRule>();
    }

    /// <summary>
    /// Represents an input column in the DMN table
    /// </summary>
    public class DmnInput
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Expression { get; set; }
        public string TypeRef { get; set; } = "string";
    }

    /// <summary>
    /// Represents an output column in the DMN table
    /// </summary>
    public class DmnOutput
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Name { get; set; }
        public string TypeRef { get; set; } = "string";
    }

    /// <summary>
    /// Represents a single rule (row) in the DMN table
    /// </summary>
    public class DmnRule
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public List<DmnInputEntry> InputEntries { get; set; } = new List<DmnInputEntry>();
        public List<DmnOutputEntry> OutputEntries { get; set; } = new List<DmnOutputEntry>();
    }

    /// <summary>
    /// Represents an input cell value in a rule
    /// </summary>
    public class DmnInputEntry
    {
        public string Id { get; set; }
        public string Text { get; set; }
    }

    /// <summary>
    /// Represents an output cell value in a rule
    /// </summary>
    public class DmnOutputEntry
    {
        public string Id { get; set; }
        public string Text { get; set; }
    }

    /// <summary>
    /// DTO for creating a new DMN rule
    /// </summary>
    public class CreateDmnRuleRequest
    {
        public string Description { get; set; }

        [Required]
        public List<string> InputValues { get; set; } = new List<string>();

        [Required]
        public List<string> OutputValues { get; set; } = new List<string>();
    }

    /// <summary>
    /// DTO for updating an existing DMN rule
    /// </summary>
    public class UpdateDmnRuleRequest
    {
        [Required]
        public string RuleId { get; set; }

        public string Description { get; set; }

        [Required]
        public List<string> InputValues { get; set; } = new List<string>();

        [Required]
        public List<string> OutputValues { get; set; } = new List<string>();
    }

    /// <summary>
    /// DTO for testing DMN evaluation
    /// </summary>
    public class DmnTestRequest
    {
        [Required]
        public Dictionary<string, object> InputVariables { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Response for DMN test evaluation
    /// </summary>
    public class DmnTestResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<Dictionary<string, object>> MatchedRules { get; set; } = new List<Dictionary<string, object>>();
    }

    /// <summary>
    /// Response wrapper for API operations
    /// </summary>
    public class DmnOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}
