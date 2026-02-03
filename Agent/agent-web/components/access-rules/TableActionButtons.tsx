"use client";

import { RuleColumns } from "@/types/access-rules";
import ActionEditButton from "./action-buttons/ActionEditButton";
import ActionDeleteButton from "./action-buttons/ActionDeleteButton";

/* ----- Types ------ */

type TableActionButtonsProps = {
  rule: RuleColumns;
};

/* ----- Action Buttons Component (Edit & Delete) for Table Rows ------ */

export default function TableActionButtons({ rule }: TableActionButtonsProps) {
  return (
    <div className="flex items-center gap-1">
      <ActionEditButton rule={rule} />
      <ActionDeleteButton rule={rule} />
    </div>
  );
}
