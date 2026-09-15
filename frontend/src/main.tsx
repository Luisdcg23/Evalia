import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import { loadSession } from './auth/session'
import { registerServiceWorker, startAutoSync } from './offline/register'
import './index.css'

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)

void registerServiceWorker()
startAutoSync(() => loadSession()?.accessToken ?? null)
