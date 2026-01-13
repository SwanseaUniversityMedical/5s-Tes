/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { TreMembershipDecision } from "./TreMembershipDecision";
export type TreUser = {
  error: boolean;
  errorMessage: string | null;
  id: number;
  submissionUserId: number;
  memberDecisions: Array<TreMembershipDecision> | null;
  archived: boolean;
  username: string | null;
  email: string | null;
};
