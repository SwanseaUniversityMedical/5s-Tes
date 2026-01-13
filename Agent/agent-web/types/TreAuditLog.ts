/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { TreMembershipDecision } from "./TreMembershipDecision";
import type { TreProject } from "./TreProject";
export type TreAuditLog = {
  error: boolean;
  errorMessage: string | null;
  id: number;
  approved: boolean;
  approvedBy: string | null;
  iPaddress: string | null;
  date: string;
  project: TreProject;
  membershipDecision: TreMembershipDecision;
};
