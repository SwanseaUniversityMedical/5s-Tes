import { z } from "zod";

export type RuleColumns = {
  ruleId: string;
  description?: string;
  inputUser?: string;
  inputProject?: string;
  inputSubmissionId?: string;
  outputTag: string;
  outputValue: string;
  outputEnv: string;
};

// Access Rules Table Action Buttons Type
export type RuleAction = "edit" | "delete" ;

// DMN Rule Data Structures
interface DmnRuleSubData {
  id?: string | null;
  text?: string | null;
}

// Interface for incoming data representing a DMN rule
export interface DmnRule {
  id?: string | null;
  description?: string | null;
  inputEntries?: Array<DmnRuleSubData> | null;
  outputEntries?: Array<DmnRuleSubData> | null;
}

// Interface for incoming data representing decision model details
export interface DecisionInfo {
  decisionId: string;
  decisionName: string;
  hitPolicy: string;
}

export interface AccessRulesData {
  rules: RuleColumns[];
  info: DecisionInfo;
}

// Zod Schema for Validating Access Rules Form Data
export const ruleFormSchema = z.object({
  ruleId: z.string().optional(),

  inputUser: z
    .string()
    .max(30, "User input must be less than 30 characters")
    .optional(),

  inputProject: z
    .string()
    .max(30, "Project input must be less than 30 characters")
    .optional(),

  outputTag: z
    .string()
    .min(1, "Output tag is required")
    .max(40, "Output tag must be less than 40 characters"),

  outputValue: z
    .string()
    .min(1, "Output value is required. Use \"\" for empty value instead.")
    .max(100, "Output value must be less than 100 characters"),

  outputEnv: z
    .string()
    .min(1, "Output environment is required")
    .max(30, "Output environment must be less than 30 characters"),

  description: z
    .string()
    .max(50, "Description must be less than 100 characters")
    .optional(),
});

// Zod Validation for Access Rules Form Data
export type RuleFormData = z.infer<typeof ruleFormSchema>;




/* ----- API Response Types ------ */

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

export interface DmnDecisionTable {
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

export interface DmnValidateResponse {
  $id?: string;
  success: boolean;
  message: string | null;
  data: string | null;
}

export interface DmnOperationResult {
  $id?: string;
  success: boolean;
  message: string | null;
  data: any;
}

export interface UpdateDmnRuleRequest {
  ruleId: string;
  description?: string | null;
  inputValues: Array<string>;
  outputValues: Array<string>;
}

export interface CreateDmnRuleRequest {
  description?: string | null;
  inputValues: Array<string>;
  outputValues: Array<string>;
}

export interface UpdateAccessRuleDto {
  inputUser?: string;
  inputProject?: string;
  inputSubmissionId?: string;
  outputTag: string;
  outputValue: string;
  outputEnv: string;
  description?: string;
}

export interface CreateAccessRuleDto {
  inputUser?: string;
  inputProject?: string;
  outputTag: string;
  outputValue: string;
  outputEnv: string;
  description?: string;
}