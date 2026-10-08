// Page pieces for applications, so an app looks and behaves like the platform's own pages without
// re-writing the same markup: import { PageHeader, TableCard, Modal, ... } from '../../components/page';
// The platform ships the same folder; the starter kit keeps a byte-identical copy.
export { Icon, PageHeader, AddButton, type PageHeaderProps } from './PageHeader';
export { Modal, DialogButtons } from './Dialog';
export { TableCard, CardHeader, IconAction, SortTh, nextSort, sortRows, type Sort } from './Table';
export { SelectField, TextField, TextAreaField, FilterCard, SearchField, parseNum, numText, type SelectOption } from './Fields';
export { Tabs } from './Tabs';
export { ImageViewer } from './ImageViewer';
export { useConfirm, type ConfirmOptions } from './useConfirm';
