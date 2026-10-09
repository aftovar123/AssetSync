import '@testing-library/jest-dom/vitest'

// jsdom has <dialog> but not its modal methods; a minimal stand-in that
// toggles the open attribute is enough for components that call them.
if (!HTMLDialogElement.prototype.showModal) {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true
  }
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false
    this.dispatchEvent(new Event('close'))
  }
}
