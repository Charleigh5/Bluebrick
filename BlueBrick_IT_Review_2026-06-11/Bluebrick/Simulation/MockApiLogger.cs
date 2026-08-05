using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace BlueBrick.Simulation
{
    public class MockApiLogger
    {
        private readonly BindingList<string> _entries = new BindingList<string>();

        public BindingList<string> Entries => _entries;

        public void Log(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _entries.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
            while (_entries.Count > 500)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }
        }
    }
}
