using System;
using System.Collections.Generic;

namespace BlueBrick.Simulation
{
    public enum MockDocumentType
    {
        Part,
        Assembly,
        Drawing
    }

    public class MockDocument
    {
        private readonly List<string> _history = new List<string>();

        public MockDocument(MockDocumentType documentType, string name)
        {
            DocumentType = documentType;
            Name = name;
            CreatedAt = DateTime.Now;
            State = "Ready";
            RegisterHistory("Document created");
        }

        public string Name { get; private set; }

        public MockDocumentType DocumentType { get; }

        public string State { get; private set; }

        public DateTime CreatedAt { get; }

        public IReadOnlyList<string> History => _history.AsReadOnly();

        public void Rename(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                return;
            }

            RegisterHistory($"Renamed from {Name} to {newName}");
            Name = newName;
        }

        public void UpdateState(string newState)
        {
            State = newState;
            RegisterHistory($"State changed to {newState}");
        }

        public override string ToString()
        {
            return $"{DocumentType} - {Name} ({State})";
        }

        private void RegisterHistory(string message)
        {
            _history.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (_history.Count > 100)
            {
                _history.RemoveAt(0);
            }
        }
    }
}
