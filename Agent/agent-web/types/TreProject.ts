/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import { Decision } from "./Decision";
import type { TreAuditLog } from "./TreAuditLog";
import type { TreMembershipDecision } from "./TreMembershipDecision";
export type TreProject = {
  error: boolean;
  errorMessage: string | null;
  id: number;
  submissionProjectId?: number;
  userName: string | null;
  password: string | null;
  submissionProjectName: string | null;
  description: string | null;
  memberDecisions: Array<TreMembershipDecision> | null;
  localProjectName: string | null;
  decision: Decision;
  archived: boolean;
  approvedBy: string | null;
  lastDecisionDate: string;
  projectExpiryDate: string;
  submissionBucketTre: string | null;
  outputBucketTre: string | null;
  auditLogs: Array<TreAuditLog> | null;
  outputBucketSub: string | null;
};
