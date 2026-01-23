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
const decisionMap: Record<number, { label: string; color: string; badgeVariant: "default" | "secondary" | "destructive" | "outline" | "info" | "warning" | "success" | "error" | null | undefined }> = {
  0: { label: "Pending", color: "text-yellow-600", badgeVariant: "warning" },
  1: { label: "Approved", color: "text-green-600", badgeVariant: "success" },
  2: { label: "Rejected", color: "text-red-600", badgeVariant: "error" },
};

export const getDecisionInfo = (decision: Decision) =>
  decisionMap[decision as number] || {
    label: `Unknown (${decision})`,
    color: "text-muted-foreground",
    badgeVariant: "default",
  };
