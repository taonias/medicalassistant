// Public surface of the chat module (R20): the router entry point plus each
// generation's own barrel. `conversations` is the current stateful doctor↔AI
// experience; `legacy` and `legacy-actions` are earlier chat/action-trigger
// paths kept intact and quarantined here rather than deleted, so developers
// immediately know which generation they're touching and new work doesn't
// accidentally extend a legacy path.
export { ChatPage } from './pages/ChatPage';
export * from './conversations';
export * from './legacy';
export * from './legacy-actions';
