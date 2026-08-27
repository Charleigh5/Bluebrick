export type AssistantSurface = "default" | "execution-board" | "vira-lab" | "hardware-cad";

export function resolveAssistantSurface(search: string): AssistantSurface {
  const mode = new URLSearchParams(search).get("mode");
  if (mode === "execution-board" || mode === "vira-lab" || mode === "hardware-cad") return mode;
  return "default";
}
