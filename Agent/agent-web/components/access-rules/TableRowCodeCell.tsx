import { cn } from "@/lib/utils";


/* ----- Types ------ */
type CodeCellProps = {
  children: React.ReactNode;
  className?: string;
};

/* ----- Code Cell Component (renders table
row contents in a code style) ------ */
export function CodeCell({ children, className }: CodeCellProps) {
  return (
      <pre
      className={cn(
        "px-1.5 py-0.5 rounded text-sm font-mono",
        "bg-red-50 text-red-500",
        "dark:bg-red-950/50 dark:text-red-400",
        className
      )}
    >
      {children}
    </pre>
  );
}