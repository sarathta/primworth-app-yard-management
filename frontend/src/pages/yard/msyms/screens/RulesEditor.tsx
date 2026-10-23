import { useCallback, useEffect, useRef, useState } from 'react';
import type { Monaco } from '@monaco-editor/react';
import { loader } from '@monaco-editor/react';
import { ConfigProvider } from 'antd';
import * as monaco from 'monaco-editor';
import editorWorker from 'monaco-editor/esm/vs/editor/editor.worker?worker';
import cssWorker from 'monaco-editor/esm/vs/language/css/css.worker?worker';
import htmlWorker from 'monaco-editor/esm/vs/language/html/html.worker?worker';
import jsonWorker from 'monaco-editor/esm/vs/language/json/json.worker?worker';
import tsWorker from 'monaco-editor/esm/vs/language/typescript/ts.worker?worker';
import initZen from '@gorules/zen-engine-wasm';
import '@gorules/jdm-editor/dist/style.css';
import { DecisionGraph, JdmConfigProvider, type DecisionGraphType } from '@gorules/jdm-editor';
import { Save } from 'lucide-react';
import { BASE_URL, authGet, authPut } from '../../../../api';
import type { ApiResponse } from '../../../../types';
import { quietGorulesConsole } from './quietGorulesConsole';

declare global {
  interface Window {
    monaco?: Monaco;
  }
}

self.monaco = monaco;
self.MonacoEnvironment = {
  getWorker(_workerId: string, label: string) {
    if (label === 'json') return new jsonWorker();
    if (label === 'css' || label === 'scss' || label === 'less') return new cssWorker();
    if (label === 'html' || label === 'handlebars' || label === 'razor') return new htmlWorker();
    if (label === 'typescript' || label === 'javascript') return new tsWorker();
    return new editorWorker();
  },
};
loader.config({ monaco });
quietGorulesConsole();

const emptyGraph: DecisionGraphType = { nodes: [], edges: [] };
const darkTheme = { mode: 'dark' as const };
const noCustomNodes: [] = [];

function messageFrom(err: unknown): string {
  const text = err instanceof Error ? err.message : String(err);
  try {
    const parsed = JSON.parse(text) as Partial<ApiResponse<unknown>>;
    if (typeof parsed.message === 'string') return parsed.message;
  } catch { /* the body was not the API envelope */ }
  return text || 'Could not save the rules.';
}

export default function RulesEditor() {
  const graphRef = useRef<DecisionGraphType>(emptyGraph);
  const [initial, setInitial] = useState<DecisionGraphType | null>(null);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        await initZen();
        const res = await authGet<ApiResponse<DecisionGraphType>>(`${BASE_URL}/api/yard/rules`);
        const data = res.data && Array.isArray(res.data.nodes) && Array.isArray(res.data.edges) ? res.data : emptyGraph;
        graphRef.current = data;
        if (!cancelled) setInitial(data);
      } catch (err) {
        graphRef.current = emptyGraph;
        if (!cancelled) {
          setError(messageFrom(err));
          setInitial(emptyGraph);
        }
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const onGraphChange = useCallback((val: DecisionGraphType) => {
    graphRef.current = val;
  }, []);

  const save = async () => {
    setSaving(true);
    setNotice('');
    setError('');
    try {
      const res = await authPut<ApiResponse<null>>(`${BASE_URL}/api/yard/rules`, graphRef.current);
      setNotice(res.message || 'Rules saved.');
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div>
      <div className="flex items-center justify-between gap-4 mb-4">
        <p></p>
        <div className="flex items-center gap-3 shrink-0">
          {notice && <span className="text-[10px] uppercase tracking-widest text-success">{notice}</span>}
          {error && <span className="text-[10px] text-danger max-w-sm">{error}</span>}
          <button
            type="button"
            onClick={save}
            disabled={saving || !initial}
            className="flex items-center gap-2 bg-accent text-background font-bold px-4 py-2 uppercase tracking-widest text-xs rounded hover:opacity-80 disabled:opacity-40"
          >
            <Save size={14} /> {saving ? 'Saving' : 'Save'}
          </button>
        </div>
      </div>
      <div className="rules-graph rounded-xl border border-[#333] overflow-hidden bg-[#141414]">
        {initial ? (
          <JdmConfigProvider theme={darkTheme}>
            <ConfigProvider wave={{ disabled: true }}>
              <DecisionGraph defaultValue={initial} onChange={onGraphChange} customNodes={noCustomNodes} />
            </ConfigProvider>
          </JdmConfigProvider>
        ) : (
          <div className="h-full flex items-center justify-center text-sm text-secondary">Loading rules...</div>
        )}
      </div>
      <style>{`.rules-graph { height: 72vh; min-height: 560px; } .rules-graph > div { height: 100%; }`}</style>
    </div>
  );
}
