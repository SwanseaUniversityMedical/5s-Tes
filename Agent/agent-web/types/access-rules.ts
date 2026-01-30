export type RuleColumns = {
  description: string;
  inputUser: string;
  inputProject: string;
  outputTag: string;
  outputValue: string;
  outputEnv: string;
};

export type RuleAction = "edit" | "delete" | "save";


interface DmnRuleSubData {
  id?: string | null;
  text?: string | null;
}

export interface DmnRule {
  id?: string | null;
  description?: string | null;
  inputEntries?: Array<DmnRuleSubData> | null;
  outputEntries?: Array<DmnRuleSubData> | null;
}


export interface DecisionInfo {
  decisionId: string;
  decisionName: string;
  hitPolicy: string;
}

export interface AccessRulesData {
  rules: RuleColumns[];
  info: DecisionInfo;
}