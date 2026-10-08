import type { ReactNode } from 'react';

// ── Backend read shapes (returned by GET /tables/:name) ──────────────────────

export interface ColumnSchema {
  dbName: string;
  displayName?: string;
  dataType: string;
  maxLength: number | null;
  precision: number | null;
  scale: number | null;
  isPrimaryKey: boolean;
  isIdentity: boolean;
  isNullable: boolean;
  isForeignKey: boolean;
  refTable: string | null;
  refColumn: string | null;
  refDisplayColumn: string | null;
  refDisplayColumns?: string[];
  defaultValue: string | null;
  isUnique?: boolean;
  joinTable: string | null;
  joinLocalColumn: string | null;
  joinParentColumn: string | null;
}

export interface TableSchema {
  dbName: string;
  displayName: string;
  columns: ColumnSchema[];
  primaryKeyConstraintName?: string;
  selectedApps?: string[];
  // Generate Tables classification (dictionary/instance/recipe) — single value, optional.
  generationTag?: string;
}

// ── Schema copy config (returned by GET /api/Project/GetTableCopyConfig) ─────

export interface SchemaTableCopyRule {
  to: 'project' | 'sub_project';
  type: 'prefix' | 'tablename';
  to_copy: string;
  editable: boolean;         // false → edit/truncate/insert/import/export-csv disabled
  open_table: boolean;       // false → Open Table button disabled
  hide: boolean;             // true  → table hidden from project/sub-project master list
  droppable?: boolean | null; // null/undefined → falls back to `editable`; set to decouple Drop Table from editable
}

// ── Column/table dependency config (GET /api/Project/GetSchemaDependencyRules) ─
// Finer-grained than SchemaTableCopyRule: locks a single table name or a single
// column (rename/retype/drop) that application code references by name, while
// leaving the rest of that table's Edit Table dialog fully usable.
export interface SchemaDependencyRule {
  type: 'table' | 'column';
  tableMatch: 'tablename' | 'any'; // 'any' → column rule applies to every table
  table: string;                    // ignored when tableMatch === 'any'
  column?: string | null;           // set when type === 'column'
  editable: boolean;
  typeEditable: boolean;            // false → Data Type / Length / Precision locked (column rules only)
  requiredEditable: boolean;        // false → ONE-WAY lock: Mandatory can be checked but not unchecked once set
  deletable: boolean;
  viewable: boolean;
  insertable: boolean;
  reason: string;
}

// ── Frontend write shapes (sent to backend for CREATE / ALTER) ────────────────

export interface ColumnDefinition {
  dbName: string;
  originalDbName?: string;
  displayName?: string;
  dataType: string;
  maxLength: number | null;
  precision: number | null;
  scale: number | null;
  isPrimaryKey: boolean;
  isIdentity: boolean;
  isRequired: boolean;
  defaultValue: string;
  isUnique: boolean;
}

export interface ForeignKeyDef {
  localColumn: string;
  parentTable: string;
  parentColumn: string;
  refDisplayColumn: string;
  refDisplayColumns?: string[];
  constraintName: string;
  joinTable?: string;
  joinLocalColumn?: string;
  joinParentColumn?: string;
  isJoin?: boolean;
}

export interface TableDefinition {
  dbName: string;
  displayName?: string;
  columns: ColumnDefinition[];
  foreignKeys: ForeignKeyDef[];
}

// ── API response wrappers ─────────────────────────────────────────────────────

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T | null;
}

export interface PagedData {
  rows: Record<string, unknown>[];
  total: number;
  page: number;
  pageSize: number;
}

export interface QueryPreviewResponse {
  success: boolean;
  sql: string;
  error?: string;
}

export interface CsvImportResult {
  success: boolean;
  message: string;
  inserted: number;
  failed: number;
  errors: string[];
  skippedCsvContent?: string;
  skippedFileName?: string;
}

export interface DataTypeDef {
  id: number;
  postgresType: string;
}

// ── Component prop interfaces ─────────────────────────────────────────────────

export interface ConfirmDialogProps {
  title?: string;
  message?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
  onConfirm?: () => void;
  onCancel?: () => void;
  children?: ReactNode;
  // Optional width override for the modal box (e.g. 560 or '600px').
  width?: number | string;
}

export interface SqlPreviewFooterProps {
  sql?: string;
  busy?: boolean;
  onExecute?: () => void;
  title?: string;
  emptyMessage?: string;
  readOnly?: boolean; // hides the Execute button — Copy/Download only
  extraActions?: ReactNode; // extra buttons rendered after Copy/Download/Execute
}

// ── Page-internal types ───────────────────────────────────────────────────────

export interface ImportPreviewItem {
  def: TableDefinition;
  sql: string;
}

export type FkLabelsMap = Record<string, Record<string, string>>;

/** Grid column — physical schema column or virtual join-display field. */
export interface GridColumn extends ColumnSchema {
  isJoinDisplay?: boolean;
  joinDisplayField?: string;
}

export interface LookupOption {
  value: unknown;
  label: string;
}

export interface CsvColumnMapping {
  csvHeader: string;
  col: ColumnSchema | null;
  matchedBy: 'dbName' | null;
}

export interface CsvRowError {
  rowNum: number;
  msg: string;
}

export interface CsvImportState {
  mappings: CsvColumnMapping[];
  missingRequired: ColumnSchema[];
  rows: Record<string, string>[];
  previewRows: Record<string, string>[];
  previewCols: ColumnSchema[];
  rowErrors: CsvRowError[];
  totalRows: number;
}
