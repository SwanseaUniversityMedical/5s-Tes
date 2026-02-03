import { DmnRule, RuleColumns } from "@/types/access-rules";

{/* ----- Helper Functions for Access-Rules API Client */}



// Helper: Convert input text to display value
function _formatInputValueForDisplay(text: string | null | undefined): string {

  /* Helper to format input values for displaying
    in the table and mapping special cases.

    Normally, the `Dmn/rules` endpoint returns
    "-" for "any" and "" for empty/undefined values.

    This function maps these values as follows:

        - Converts "-" to "Any"
        - Converts undefined/null/empty to "Empty"

    - Otherwise, simply returns the original text.
  */

  if (text === null || text === undefined || text === "") {
    return "Empty";
  }
  if (text === "-") {
    return "Any";
  }
  return text;
}

// Transform Rules to Table Format
export function transformRulesToRuleColumns(rules: DmnRule[]): RuleColumns[] {

  /* Fixed indices based on API structure and
    maps the formatted Input Value (project/user)
    to the Input:user and Input:project columns.

  - Inputs:
    - 0 = project,
    - 1 = user,
    - 2 = submissionId (not displayed)

  - Outputs:
    - 0 = tag,
    - 1 = value,
    - 2 = env
  */
  return rules.map((rule) => ({
    ruleId: rule.id ?? "",
    description: rule.description ?? "-",
    inputUser: _formatInputValueForDisplay(rule.inputEntries?.[1]?.text),
    inputProject: _formatInputValueForDisplay(rule.inputEntries?.[0]?.text),
    outputTag: rule.outputEntries?.[0]?.text ?? "",
    outputValue: rule.outputEntries?.[1]?.text ?? "",
    outputEnv: rule.outputEntries?.[2]?.text ?? "",
  }));
}

// Helper: Convert display value to API input text (for PUT/POST)
function _formatInputValueForApi(text: string | null | undefined): string {
  /*
    Transforms display values back to API format.

    Display shows:
      - "Any"
      - "Empty"

    API expects:
      - "-" for "any"
      - "" for empty
  */
  if (text === null || text === undefined || text === "Empty" || text === "") {
    return "";
  }
  if (text === "Any" || text === "-") {
    return "-";
  }
  return text;
}

/* ----- PUT/POST: Transform Form Data to API Format ----- */

export function transformFormDataForApi(data: {
  inputUser?: string;
  inputProject?: string;
  outputTag: string;
  outputValue: string;
  outputEnv: string;
}): {
  inputValues: string[];
  outputValues: string[];
} {
  /*
    Transforms form data to API request format.

    Input array order (matches API structure):
      - [0] = project
      - [1] = user

    Output array order:
      - [0] = tag
      - [1] = value
      - [2] = env
  */
  return {
    inputValues: [
      _formatInputValueForApi(data.inputProject),
      _formatInputValueForApi(data.inputUser),
    ],
    outputValues: [
      data.outputTag,
      data.outputValue,
      data.outputEnv,
    ],
  };
}