"use client";

import { useState } from "react";
import {
  ColumnDef,
  SortingState,
  PaginationState,
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  flexRender,
} from "@tanstack/react-table";

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

import { SortableHeader } from "./TableColumnSortable";
import { BaseTableControls } from "./BaseTableControls";
import { BaseTableFooter } from "./BaseTableFooter";

/* ----- Types & Constants------ */

type BaseTableProps<TData> = {
  data: TData[];
  columns: ColumnDef<TData, any>[];
  defaultPageSize?: number;
  searchPlaceholder?: string;
  emptyMessage?: string;
  renderFooterRow?: (colSpan: number) => React.ReactNode;
  renderToolbar?: () => React.ReactNode;
  projectListingPage?: boolean;
};

const DEFAULT_PAGE_SIZE = 20;

/* ----- Base Table Component ------ */

export function BaseTable<TData>({
  data,
  columns,
  defaultPageSize = DEFAULT_PAGE_SIZE,
  searchPlaceholder = "Search...",
  emptyMessage = "No results found.",
  renderFooterRow,
  renderToolbar,
}: BaseTableProps<TData>) {

  // Helpers for table state
  const [globalFilter, setGlobalFilter] = useState("");
  const [sorting, setSorting] = useState<SortingState>([]);
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: defaultPageSize,
  });

  const table = useReactTable({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    onSortingChange: setSorting,
    onPaginationChange: setPagination,
    state: {
      globalFilter,
      sorting,
      pagination,
    },
    onGlobalFilterChange: setGlobalFilter,
  });

  const { pageIndex, pageSize } = pagination;
  const totalRows = table.getFilteredRowModel().rows.length;
  const totalPages = table.getPageCount();
  const showingFrom = totalRows > 0 ? pageIndex * pageSize + 1 : 0;
  const showingTo = Math.min((pageIndex + 1) * pageSize, totalRows);

  return (
    <div className="w-full">
      {/* ---- Controls ---- */}
      <BaseTableControls
        pageSize={pageSize}
        onPageSizeChange={(size) =>
          setPagination((prev) => ({ ...prev, pageSize: size, pageIndex: 0 }))
        }
        searchValue={globalFilter}
        onSearchChange={setGlobalFilter}
        searchPlaceholder={searchPlaceholder}
        renderToolbar={renderToolbar}
      />

      {/* ---- Table Container ---- */}
      <div className="rounded-lg border bg-background px-2">
        <Table>

          {/* ---- Table Header ---- */}
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map((header) => {
                  const sortDirection = header.column.getIsSorted();
                  return (
                    <TableHead
                      key={header.id}
                      className="text-sm font-semibold text-foreground py-3"
                    >
                      {header.isPlaceholder ? null : (
                        <SortableHeader
                          key={`${header.id}-${sortDirection}`}
                          column={header.column}
                          title={String(header.column.columnDef.header ?? "")}
                        />
                      )}
                    </TableHead>
                  );
                })}
              </TableRow>
            ))}
          </TableHeader>

          {/* ---- Table Body ---- */}
          <TableBody>
            {table.getRowModel().rows.length ? (
              <>
                {table.getRowModel().rows.map((row) => (
                  <TableRow key={row.id} className="hover:bg-muted/50">
                    {row.getVisibleCells().map((cell) => (
                      <TableCell key={cell.id}>
                        {flexRender(
                          cell.column.columnDef.cell,
                          cell.getContext(),
                        )}
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
                {/* Custom Footer Row (e.g., Add Button) */}
                {renderFooterRow?.(columns.length)}
              </>
            ) : (
              <TableRow>
                <TableCell
                  colSpan={columns.length}
                  className="h-24 text-center"
                >
                  {emptyMessage}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      {/* ---- Footer ---- */}
      <BaseTableFooter
        totalRows={totalRows}
        currentPage={pageIndex + 1}
        totalPages={totalPages}
        showingFrom={showingFrom}
        showingTo={showingTo}
        canPreviousPage={table.getCanPreviousPage()}
        canNextPage={table.getCanNextPage()}
        onPreviousPage={() => table.previousPage()}
        onNextPage={() => table.nextPage()}
      />
    </div>
  );
}
