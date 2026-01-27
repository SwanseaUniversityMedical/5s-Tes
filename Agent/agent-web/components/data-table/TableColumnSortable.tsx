"use client";

import { Column } from "@tanstack/react-table";
import { ArrowUp, ArrowDown } from "lucide-react";
import { Button } from "@/components/ui/button";

/* ----- Types ------ */

type SortableHeaderProps<T> = {
  column: Column<T, unknown>;
  title: string;
};

/* ----- Sortable Header Component for Table Columns ------ */

export function SortableHeader<T>({ column, title }: SortableHeaderProps<T>) {
  const isSorted = column.getIsSorted();

  if (!column.getCanSort()) {
    return <div className="font-semibold">{title}</div>;
  }

  return (
    <Button
      variant="ghost"
      onClick={column.getToggleSortingHandler()}
      className="w-full justify-start p-0 h-auto font-semibold hover:bg-transparent"
    >
      <span className="flex items-center gap-1">
        {title}
        {isSorted === "desc" ? (
          <ArrowDown className="h-4 w-4 text-black" />
        ) : (
          <ArrowUp
            className={`h-4 w-4 ${
              isSorted === "asc" ? "text-black" : "text-gray-400"
            }`}
          />
        )}
      </span>
    </Button>
  );
}
