// Tiny notification "ping" generated with the Web Audio API — no asset to bundle, works offline.
// Two soft descending tones. Silently no-ops if audio isn't available or the tab hasn't been interacted with.
let ctx: AudioContext | null = null

export function playPing(): void {
  try {
    const AC = window.AudioContext || (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
    if (!AC) return
    ctx ??= new AC()
    // Browsers require a user gesture before audio can start; if still suspended, skip quietly.
    if (ctx.state === 'suspended') { void ctx.resume().catch(() => {}) }

    const now = ctx.currentTime
    const notes = [880, 660]
    notes.forEach((freq, i) => {
      const osc = ctx!.createOscillator()
      const gain = ctx!.createGain()
      osc.type = 'sine'
      osc.frequency.value = freq
      const t = now + i * 0.12
      gain.gain.setValueAtTime(0.0001, t)
      gain.gain.exponentialRampToValueAtTime(0.12, t + 0.02)
      gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.18)
      osc.connect(gain).connect(ctx!.destination)
      osc.start(t)
      osc.stop(t + 0.2)
    })
  } catch {
    // Audio unavailable — ignore.
  }
}
