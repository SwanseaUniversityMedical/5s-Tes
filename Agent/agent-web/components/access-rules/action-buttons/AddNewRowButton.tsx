"use client";

import { useState } from "react";
import { Plus } from "lucide-react";
import { TableCell, TableRow } from "@/components/ui/table";
import { RuleColumns } from "@/types/access-rules";
import RuleFormDialog from "../forms/RulesFormDialog";

/* ----- Types & Constants ----- */

type HoverAddRowProps = {
  colSpan: number;
  onAdd: (newRule: RuleColumns) => void;
  label?: string;
};

// Row styles
const ROW_STYLES = {
  base: "group cursor-pointer bg-transparent hover:bg-transparent border-none",
};

// Cell styles
const CELL_STYLES = {
  base: "p-0 pt-4 h-12 relative",
};

// Container styles (holds lines + button)
const CONTAINER_STYLES = {
  base: "absolute inset-x-0 top-1/2 -translate-y-1/2 flex items-center justify-center",
};

/* ----- Line Styles ----- */

// Line styles - BEFORE hover (default state)
const LINE_DEFAULT_STYLES = {
  width: "w-12",
  height: "h-px",
  color: "bg-gray-300",
};

// Line styles - AFTER hover
const LINE_HOVER_STYLES = {
  width: "group-hover:w-[25%]",
  height: "group-hover:h-0.5",
  color: "group-hover:bg-blue-400",
};

// Line animation
const LINE_ANIMATION = {
  transition: "transition-all duration-300 ease-out",
};

/* ----- Button Styles ----- */

// Button styles - BEFORE hover (default state)
const BUTTON_DEFAULT_STYLES = {
  layout: "flex items-center gap-2",
  padding: "px-3 py-1 mx-2",
  shape: "rounded-full",
  background: "bg-gray-200",
  text: "text-black text-xs font-medium",
};

// Button styles - AFTER hover
const BUTTON_HOVER_STYLES = {
  background: "group-hover:bg-blue-500",
  text: "group-hover:text-white",
  shadow: "group-hover:shadow-sm",
};

// Button animation
const BUTTON_ANIMATION = {
  transition: "transition-all duration-200",
};

// Icon styles
const ICON_STYLES = {
  size: "h-3 w-3",
};

/* ----- Combined Classes ----- */

const lineClasses = `
  ${LINE_DEFAULT_STYLES.width}
  ${LINE_DEFAULT_STYLES.height}
  ${LINE_DEFAULT_STYLES.color}
  ${LINE_HOVER_STYLES.width}
  ${LINE_HOVER_STYLES.height}
  ${LINE_HOVER_STYLES.color}
  ${LINE_ANIMATION.transition}
`;

const buttonClasses = `
  ${BUTTON_DEFAULT_STYLES.layout}
  ${BUTTON_DEFAULT_STYLES.padding}
  ${BUTTON_DEFAULT_STYLES.shape}
  ${BUTTON_DEFAULT_STYLES.background}
  ${BUTTON_DEFAULT_STYLES.text}
  ${BUTTON_HOVER_STYLES.background}
  ${BUTTON_HOVER_STYLES.text}
  ${BUTTON_HOVER_STYLES.shadow}
  ${BUTTON_ANIMATION.transition}
`;

/* ----- Add New Row Hover Component ----- */

export function HoverAddRow({
  colSpan,
  onAdd,
  label = "Add new row",
}: HoverAddRowProps) {
  const [isDialogOpen, setIsDialogOpen] = useState(false);

  const handleRowClick = () => {
    setIsDialogOpen(true);
  };

  const handleSubmit = (newRule: Omit<RuleColumns, "id">) => {
    onAdd(newRule);
  };

  return (
    <>
      <TableRow onClick={handleRowClick} className={ROW_STYLES.base}>
        <TableCell colSpan={colSpan} className={CELL_STYLES.base}>
          <div className={CONTAINER_STYLES.base}>
            {/* Left Line */}
            <div className={lineClasses} />

            {/* Plus Button */}
            <div className={buttonClasses}>
              <Plus className={ICON_STYLES.size} />
              <span>{label}</span>
            </div>

            {/* Right Line */}
            <div className={lineClasses} />
          </div>
        </TableCell>
      </TableRow>

      {/* Add New Rule Dialog */}
      <RuleFormDialog
        isOpen={isDialogOpen}
        onOpenChange={setIsDialogOpen}
        onSubmit={handleSubmit}
        mode="add"
      />
    </>
  );
}
