import { Component, type ErrorInfo, type ReactNode } from "react";
import { Link } from "react-router-dom";

interface Props {
  children: ReactNode;
}

interface State {
  error: Error | null;
}

/** Keeps admin shell visible when a section throws. */
export class AdminErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error("Admin section crashed", error, info.componentStack);
  }

  render() {
    if (this.state.error) {
      return (
        <div className="portal-card">
          <h2>Something went wrong</h2>
          <p className="portal-error">{this.state.error.message}</p>
          <div className="portal-table-actions">
            <button type="button" className="btn btn--accent" onClick={() => this.setState({ error: null })}>
              Try again
            </button>
            <Link className="btn btn--ghost" to="/admin">
              Back to dashboard
            </Link>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
