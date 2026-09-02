export function createLatestRequestGuard() {
  let latestRequestId = 0;

  return {
    begin() {
      return ++latestRequestId;
    },
    isCurrent(requestId: number) {
      return requestId === latestRequestId;
    },
  };
}
