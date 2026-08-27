export type EngineeringQuerySource = "browser-ui" | "test-harness" | "contract-adapter";

export type EngineeringQuery = {
  request: string;
  source: EngineeringQuerySource;
  noExternalEgress: true;
};

export function engineeringQueryFromText(request: string, source: EngineeringQuerySource = "browser-ui"): EngineeringQuery {
  return {
    request,
    source,
    noExternalEgress: true
  };
}
