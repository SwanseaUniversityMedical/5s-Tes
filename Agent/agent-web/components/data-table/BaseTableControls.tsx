"use client";

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { Search } from "lucide-react";

/* ----- Types & Constants ------ */

type BaseTableControlsProps = {
  pageSize: number;
  onPageSizeChange: (size: number) => void;
  searchValue: string;
  onSearchChange: (value: string) => void;
  searchPlaceholder?: string;
  renderToolbar?: () => React.ReactNode;
};

const PAGE_SIZE_OPTIONS = ["10", "15", "20", "25"];

/* ----- Base Table Controls Component (Search Bar & Pagination) ------ */

export function BaseTableControls({
  pageSize,
  onPageSizeChange,
  searchValue,
  onSearchChange,
  searchPlaceholder = "Search...",
  renderToolbar,
}: BaseTableControlsProps) {
  return (
    <div className="flex items-center justify-between gap-3 py-3">

      {/* Left side: Search and pagination */}
      <div className="flex items-center gap-3">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder={searchPlaceholder}
            value={searchValue}
            onChange={(e) => onSearchChange(e.target.value)}
            className="h-9 w-64 pl-10 border-b border-border text-foreground placeholder:text-muted-foreground"
          />
        </div>

        <div className="flex items-center gap-2 text-sm text-foreground">
          <span>Show</span>
          <Select
            value={String(pageSize)}
            onValueChange={(value) => onPageSizeChange(Number(value))}
          >
            <SelectTrigger className="h-9 w-16 border-b border-border px-2 shadow-none">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {PAGE_SIZE_OPTIONS.map((size) => (
                <SelectItem key={size} value={size}>
                  {size}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <span>results</span>
        </div>
      </div>

      {/* (optional): Right side: Toolbar */}
      {renderToolbar && renderToolbar()}
    </div>
  );
}
