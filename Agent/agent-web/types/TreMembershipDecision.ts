/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type{ Decision } from "./Decision";
import type { TreAuditLog } from "./TreAuditLog";
import type { TreProject } from "./TreProject";
import type { TreUser } from "./TreUser";

export type TreMembershipDecision = {
  error: boolean;
  errorMessage: string | null;
  id: number;
  user: TreUser;
  project: TreProject;
  archived: boolean;
  decision: Decision;
  projectExpiryDate: string;
  approvedBy: string | null;
  lastDecisionDate: string;
  auditLogs: Array<TreAuditLog> | null;
};
