import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
// The platform's stylesheets, unchanged. Application-specific sheets (recipe, reporting, …)
// are not part of the kit; an application that needs its own adds one file next to these.
import './styles/index.css';
import './styles/system.css';
import './styles/loader.css';
import App from './App';

const rootEl = document.getElementById('root');
if (!rootEl) throw new Error('Root element #root not found in index.html');
createRoot(rootEl).render(
  <BrowserRouter>
    <App />
  </BrowserRouter>,
);
