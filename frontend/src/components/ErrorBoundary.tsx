import { Component, type ErrorInfo, type ReactNode } from 'react';
import { ServerError } from './Page';

interface Props {
  children: ReactNode;
  /** Changing it (e.g. the route) clears the error, so navigating away recovers. */
  resetKey?: string;
}

/** Catches a render error in a page and shows "Something went wrong" instead of a blank screen. */
export class ErrorBoundary extends Component<Props, { error: unknown; key?: string }> {
  state: { error: unknown; key?: string } = { error: null, key: this.props.resetKey };

  static getDerivedStateFromError(error: unknown) {
    return { error };
  }

  static getDerivedStateFromProps(props: Props, state: { error: unknown; key?: string }) {
    return props.resetKey !== state.key ? { error: null, key: props.resetKey } : null;
  }

  componentDidCatch(error: unknown, info: ErrorInfo) {
    console.error('Page crashed', error, info.componentStack);
  }

  render() {
    if (this.state.error) return <ServerError error={this.state.error} onRetry={() => this.setState({ error: null })} />;
    return this.props.children;
  }
}
