import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import App from './App';
import { AuthProvider } from './auth/AuthContext';
import { DemoBanner } from './demo/DemoBanner';
import './index.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <DemoBanner />
        <App />
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>,
);
