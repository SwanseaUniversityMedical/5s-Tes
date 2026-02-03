"use client";

import { Button } from "@/components/ui/button";
import { ChevronLeft, ChevronRight } from "lucide-react";

/* ----- Types ------ */

type BaseTableFooterProps = {
  totalRows: number;
  currentPage: number;
  totalPages: number;
  showingFrom: number;
  showingTo: number;
  canPreviousPage: boolean;
  canNextPage: boolean;
  onPreviousPage: () => void;
  onNextPage: () => void;
};

/* ----- Base Table Footer Component (Pagination Info & Controls) ------ */

export function BaseTableFooter({
  totalRows,
  currentPage,
  totalPages,
  showingFrom,
  showingTo,
  canPreviousPage,
  canNextPage,
  onPreviousPage,
  onNextPage,
}: BaseTableFooterProps) {
  return (
    <div className="flex items-center pt-2">

      {/* Left - Rows count */}
      <div className="flex-1 text-sm text-muted-foreground">
        Rows: {totalRows}
      </div>

      {/* Center - Page info */}
      <div className="flex-1 flex items-center justify-center gap-4 text-sm text-muted-foreground">
        <span>
          Page: {currentPage} of {totalPages || 1}
        </span>
        <span>Showing: {totalRows ? `${showingFrom}–${showingTo}` : "0"}</span>
      </div>

      {/* Right - Pagination buttons */}
      <div className="flex-1 flex items-center justify-end gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={onPreviousPage}
          disabled={!canPreviousPage}
        >
          <ChevronLeft className="h-4 w-4 mr-1" />
          Previous
        </Button>
        <Button
          variant="outline"
          size="sm"
          onClick={onNextPage}
          disabled={!canNextPage}
        >
          Next
          <ChevronRight className="h-4 w-4 ml-1" />
        </Button>
      </div>
    </div>
  );
}
