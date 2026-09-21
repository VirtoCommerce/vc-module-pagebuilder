export interface LatestRequest {
  isCurrent: () => boolean;
  complete: () => void;
}

export interface LatestRequestTracker {
  begin: () => LatestRequest;
  invalidate: () => void;
  dispose: () => void;
}

export function createLatestRequestTracker(onPendingChange: (pending: boolean) => void): LatestRequestTracker {
  let sequence = 0;
  let disposed = false;

  function begin(): LatestRequest {
    const requestId = ++sequence;
    let completed = false;
    onPendingChange(true);

    return {
      isCurrent: () => !disposed && requestId === sequence,
      complete: () => {
        if (completed) {
          return;
        }

        completed = true;
        if (!disposed && requestId === sequence) {
          onPendingChange(false);
        }
      },
    };
  }

  function invalidate() {
    sequence++;
    onPendingChange(false);
  }

  function dispose() {
    disposed = true;
    invalidate();
  }

  return {
    begin,
    invalidate,
    dispose,
  };
}
