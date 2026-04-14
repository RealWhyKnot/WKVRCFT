/// <reference types="vite/client" />

interface Window {
  photino: {
    sendMessage: (message: string) => void
  }
  external?: {
    sendMessage: (message: string) => void
  }
  chrome?: {
    webview?: {
      postMessage: (message: string) => void
      addEventListener: (event: string, handler: (e: { data: string }) => void) => void
    }
  }
}
