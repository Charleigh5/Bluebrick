using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BlueBrick.SolidWorks.Runtime;

namespace BlueBrick.Agent
{
    // Native approval dialog: single-shot WinForms modal implementing the issuer's
    // IApprovalPrompt (Sprint 03). Per the settled design ruling (Sprint 03 story), the
    // modal runs via the frozen dispatcher: ShowAsync marshals form construction through
    // TryInvoke on the main thread, queues the modal run with Control.BeginInvoke, and
    // returns the Task promptly (Sprint 02 contract C2 semantics; dispatcher unchanged).
    // Every close path settles exactly one terminal outcome (first-terminal-wins; only an
    // explicit Approve click yields Approved) and the first settle owns teardown. The
    // dialog is intentionally unregistered: the executor ticket performs the composition
    // swap as part of activation.
    internal sealed class ApprovalDialog : Form, IApprovalPrompt
    {
        private readonly ISolidWorksMainThreadDispatcher _dispatcher;
        private readonly Action<ApprovalDialog> _showModal;

        private readonly Terminal _terminal = new Terminal();
        private readonly object _callSync = new object();
        private TaskCompletionSource<ApprovalPromptOutcome> _completion;
        private CancellationTokenRegistration _registration;
        private System.Windows.Forms.Timer _countdown;
        private Font _monoFont;
        private Font _boldFont;
        private Label _countdownLabel;
        private Button _approveButton;
        private Button _denyButton;
        private DateTime _expiresUtc;

        internal ApprovalDialog(ISolidWorksMainThreadDispatcher dispatcher)
            : this(dispatcher, null)
        {
        }

        // Test-only seam (Sprint 03 contract C2): the modal run passes through an
        // injectable show action. Production posts the real modal ShowDialog through
        // Control.BeginInvoke and owns map-plus-dispose on return; tests substitute a
        // recorder so no message pump is required.
        internal ApprovalDialog(ISolidWorksMainThreadDispatcher dispatcher, Action<ApprovalDialog> showModal)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _showModal = showModal ?? ShowModalCore;
        }

        public Task<ApprovalPromptOutcome> ShowAsync(ApprovalPrompt prompt)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));
            if (IsDisposed) return Task.FromResult(ApprovalPromptOutcome.Denied);
            if (prompt.CancellationToken.IsCancellationRequested)
            {
                TryDisposeThis();
                return Task.FromResult(ApprovalPromptOutcome.Timeout);
            }
            // The Task below is returned promptly (settled design ruling): the modal run
            // is queued on the main thread and outcomes settle the Task asynchronously.
            var completion = new TaskCompletionSource<ApprovalPromptOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool marshaled;
            try
            {
                marshaled = _dispatcher.TryInvoke(() => BeginModal(prompt, completion));
            }
            catch
            {
                TryDisposeThis();
                return Task.FromResult(ApprovalPromptOutcome.Denied);
            }
            if (!marshaled)
            {
                TryDisposeThis();
                return Task.FromResult(ApprovalPromptOutcome.Denied);
            }
            return completion.Task;
        }

        private void BeginModal(ApprovalPrompt prompt, TaskCompletionSource<ApprovalPromptOutcome> completion)
        {
            lock (_callSync)
            {
                // Single-shot: exactly one admitted ShowAsync per instance. A second call
                // fails closed on its own Task without touching the first.
                if (_completion != null || IsDisposed)
                {
                    try { completion.TrySetResult(ApprovalPromptOutcome.Denied); }
                    catch { }
                    return;
                }
                _completion = completion;
            }
            try
            {
                Configure(prompt);
                // The production modal run is queued with Control.BeginInvoke, which
                // throws without a created window handle. Force handle creation here on
                // the main thread so the show path cannot deterministically fail closed
                // (round-01 F1): every prompt must actually display.
                if (Handle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Approval dialog handle was not created.");
                }
                _registration = prompt.CancellationToken.Register(() =>
                {
                    try
                    {
                        _dispatcher.TryInvoke(() => Settle(ApprovalPromptOutcome.Timeout));
                    }
                    catch
                    {
                        // Cancellation teardown must never escape into the issuer.
                    }
                });
                if (prompt.CancellationToken.IsCancellationRequested)
                {
                    Settle(ApprovalPromptOutcome.Timeout);
                    return;
                }
                StartCountdown(prompt);
                try
                {
                    _showModal(this);
                }
                catch
                {
                    Settle(ApprovalPromptOutcome.Denied);
                }
            }
            catch
            {
                Settle(ApprovalPromptOutcome.Denied);
            }
        }

        // Production modal run: queue ShowDialog on the UI message loop without blocking
        // the caller, then map the result and dispose. Covered live (Lab smoke), not in
        // VSTest (no message pump) — declared ceiling per the design ruling.
        private static void ShowModalCore(ApprovalDialog dialog)
        {
            try
            {
                dialog.BeginInvoke(new Action(() =>
                {
                    DialogResult result;
                    try
                    {
                        result = dialog.ShowDialog();
                    }
                    catch
                    {
                        result = DialogResult.None;
                    }
                    try
                    {
                        dialog.MapModalResult(result);
                    }
                    finally
                    {
                        try { dialog.Dispose(); }
                        catch { }
                    }
                }));
            }
            catch
            {
                dialog.Settle(ApprovalPromptOutcome.Denied);
            }
        }

        // First-terminal-wins settle: the winner completes the Task and owns teardown
        // (registration + single-shot dispose). Losers still close the modal.
        internal void Settle(ApprovalPromptOutcome outcome)
        {
            if (_terminal.TrySet(outcome))
            {
                var completion = _completion;
                try
                {
                    if (completion != null) completion.TrySetResult(outcome);
                }
                catch
                {
                }
                DisposeRegistration();
                try { Dispose(); }
                catch { }
            }
            CloseModal();
        }

        // Outcome resolver (Sprint 03 contract C2/C3): callable without any modal.
        // An unset terminal after the show means the window closed without an explicit
        // decision handler (X-close, unexpected result) — including a bare OK with no
        // Approve click — which denies.
        internal void MapModalResult(DialogResult result)
        {
            if (_terminal.HasOutcome) return;
            Settle(ApprovalPromptOutcome.Denied);
        }

        private void Configure(ApprovalPrompt prompt)
        {
            _expiresUtc = prompt.ExpiresUtc;
            SuspendLayout();
            try
            {
                Text = "Approve custom property change";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterParent;
                TopMost = true;
                ShowInTaskbar = false;
                ClientSize = new Size(560, 420);

                _monoFont = new Font(FontFamily.GenericMonospace, 9);
                _boldFont = new Font(Font.FontFamily, Font.Size, FontStyle.Bold);

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    Padding = new Padding(12)
                };
                Controls.Add(layout);

                var title = new Label { AutoSize = true, Font = _boldFont };
                title.Text = prompt.Preview != null ? prompt.Preview.Title : "Approve change";
                layout.Controls.Add(title);

                var summary = new Label { AutoSize = true };
                summary.Text = prompt.Preview != null ? prompt.Preview.Summary : string.Empty;
                layout.Controls.Add(summary);

                var diff = new Label { AutoSize = true, Font = _monoFont, ForeColor = Color.Black };
                diff.Text = "OLD: " + (prompt.Preview != null ? prompt.Preview.Before : string.Empty)
                    + Environment.NewLine + "NEW: " + (prompt.Preview != null ? prompt.Preview.After : string.Empty);
                layout.Controls.Add(diff);

                var risk = new Label { AutoSize = true, Font = _boldFont, ForeColor = Color.DarkRed };
                risk.Text = prompt.Preview != null ? prompt.Preview.Risk : string.Empty;
                layout.Controls.Add(risk);

                var ids = new Label { AutoSize = true, ForeColor = Color.Gray };
                ids.Text = "session " + prompt.SessionId + " · trace " + prompt.TraceId;
                layout.Controls.Add(ids);

                _countdownLabel = new Label { AutoSize = true, ForeColor = Color.Gray };
                layout.Controls.Add(_countdownLabel);

                var footer = new Label { AutoSize = true, Font = new Font(Font.FontFamily, Font.Size, FontStyle.Italic) };
                footer.Text = "Approving executes exactly this change once. A receipt is written either way.";
                layout.Controls.Add(footer);

                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft };
                Controls.Add(buttons);

                // No AcceptButton: Enter must not approve. Only an explicit Approve click
                // yields Approved, keeping the outcome mapping exact.
                _denyButton = new Button { Text = "Deny", DialogResult = DialogResult.Cancel };
                _denyButton.Click += (sender, e) => OnDenyClicked();
                buttons.Controls.Add(_denyButton);
                CancelButton = _denyButton;

                _approveButton = new Button { Text = "Approve" };
                _approveButton.Click += (sender, e) => OnApproveClicked();
                buttons.Controls.Add(_approveButton);

                UpdateCountdown();
            }
            finally
            {
                ResumeLayout(false);
            }
        }

        internal void OnApproveClicked()
        {
            Settle(ApprovalPromptOutcome.Approved);
        }

        internal void OnDenyClicked()
        {
            Settle(ApprovalPromptOutcome.Denied);
        }

        internal void OnTimerExpired()
        {
            Settle(ApprovalPromptOutcome.Timeout);
        }

        internal ApprovalPromptOutcome CurrentOutcome
        {
            get { return _terminal.Outcome; }
        }

        private void StartCountdown(ApprovalPrompt prompt)
        {
            StopCountdown();
            _countdown = new System.Windows.Forms.Timer { Interval = 500 };
            _countdown.Tick += (sender, e) =>
            {
                UpdateCountdown();
                if (DateTime.UtcNow >= _expiresUtc)
                {
                    OnTimerExpired();
                }
            };
            _countdown.Start();
        }

        private void StopCountdown()
        {
            if (_countdown == null) return;
            try { _countdown.Stop(); }
            catch { }
            try { _countdown.Dispose(); }
            catch { }
            _countdown = null;
        }

        private void UpdateCountdown()
        {
            if (_countdownLabel == null || _countdownLabel.IsDisposed) return;
            var remaining = _expiresUtc - DateTime.UtcNow;
            var seconds = remaining.TotalSeconds > 0 ? (int)Math.Ceiling(remaining.TotalSeconds) : 0;
            _countdownLabel.Text = "Expires in " + seconds + "s — approval executes exactly once.";
            Text = "Approve custom property change (" + seconds + "s remaining)";
        }

        private void CloseModal()
        {
            try
            {
                if (IsDisposed || Disposing) return;
                // Nothing was ever shown (recorder/headless tests): no modal loop to break.
                // Skipping Close here also keeps headless tests single-threaded for UI:
                // only the thread running the show path touches window state.
                if (!IsHandleCreated && !Modal) return;
                Close();
            }
            catch
            {
                // Close on an unshown or racing form must never escape.
            }
        }

        private void DisposeRegistration()
        {
            try
            {
                var registration = _registration;
                _registration = default(CancellationTokenRegistration);
                registration.Dispose();
            }
            catch
            {
            }
        }

        private void TryDisposeThis()
        {
            try { Dispose(); }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopCountdown();
                if (_monoFont != null)
                {
                    try { _monoFont.Dispose(); }
                    catch { }
                    _monoFont = null;
                }
                if (_boldFont != null)
                {
                    try { _boldFont.Dispose(); }
                    catch { }
                    _boldFont = null;
                }
            }
            base.Dispose(disposing);
        }

        private sealed class Terminal
        {
            private readonly object _sync = new object();
            private ApprovalPromptOutcome? _outcome;

            internal bool HasOutcome
            {
                get { lock (_sync) { return _outcome.HasValue; } }
            }

            internal ApprovalPromptOutcome Outcome
            {
                get { lock (_sync) { return _outcome ?? ApprovalPromptOutcome.Denied; } }
            }

            internal bool TrySet(ApprovalPromptOutcome outcome)
            {
                lock (_sync)
                {
                    if (_outcome.HasValue) return false;
                    _outcome = outcome;
                    return true;
                }
            }
        }
    }
}
