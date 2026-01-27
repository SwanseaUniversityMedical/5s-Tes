import { getDecisionInfo } from "@/types/Decision";
import { Check, X } from "lucide-react";

export const RADIO_OPTIONS = [
  {
    label: "Approve",
    value: "1",
    icon: <Check className={`${getDecisionInfo(1).color} w-4 h-4`} />,
  },
  {
    label: "Reject",
    value: "2",
    icon: <X className={`${getDecisionInfo(2).color} w-4 h-4`} />,
  },
];
