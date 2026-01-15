/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
export enum Decision {
  "_0" = 0,
  "_1" = 1,
  "_2" = 2,
}

// Map Decision enum to readable strings
const decisionMap: Record<number, { label: string; color: string }> = {
  0: { label: "Pending", color: "text-yellow-600" },
  1: { label: "Approved", color: "text-green-600" },
  2: { label: "Rejected", color: "text-red-600" },
};

export const getDecisionInfo = (decision: Decision) =>
  decisionMap[decision as number] || {
    label: `Unknown (${decision})`,
    color: "text-muted-foreground",
  };
