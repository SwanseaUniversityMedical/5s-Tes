// api/access-rules.ts
import request from "@/lib/api/request";
import { DecisionInfo, AccessRulesData, RuleColumns } from "@/types/access-rules";
import { transformRulesToRuleColumns } from "@/lib/helpers/access-rules-api";

/* ----- API Response Types ------ */

interface DmnRuleSubData {
  id?: string | null;
  text?: string | null;
}

interface DmnRule {
  id?: string | null;
  description?: string | null;
  inputEntries?: Array<DmnRuleSubData> | null;
  outputEntries?: Array<DmnRuleSubData> | null;
}

interface DmnDecisionTable {
  decisionId?: string | null;
  decisionName?: string | null;
  hitPolicy?: string | null;
  inputs?: Array<{
    id?: string | null;
    label?: string | null;
    expression?: string | null;
    typeRef?: string | null;
  }> | null;
  outputs?: Array<{
    id?: string | null;
    label?: string | null;
    name?: string | null;
    typeRef?: string | null;
  }> | null;
  rules?: Array<DmnRule> | null;
}

/* ----- API Result Type ------ */

type ApiResult<T> =
  | { success: true; data: T }
  | { success: false; error: string };


/* ----- API Functions ------ */

//  --- GET: Api/Dmn/table -----
export async function getAccessRules(): Promise<ApiResult<AccessRulesData>> {
  try {
    const data = await request<DmnDecisionTable>("Dmn/table");
    const rules = transformRulesToRuleColumns(data.rules ?? []);
    const info: DecisionInfo = {
      decisionId: data.decisionId ?? "",
      decisionName: data.decisionName ?? "",
      hitPolicy: data.hitPolicy ?? "",
    };

    return { success: true, data: { rules, info } };
  } catch (error) {
    return {
      success: false,
      error:
        error instanceof Error ? error.message : "Failed to fetch table for access rules",
    };
  }
}