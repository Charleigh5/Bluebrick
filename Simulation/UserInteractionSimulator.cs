using System;
using System.Collections.Generic;

namespace BlueBrick.Simulation
{
    public class UserInteractionSimulator
    {
        private readonly MockSolidWorksEnvironment _environment;
        private readonly List<string> _interactionLog = new List<string>();

        public UserInteractionSimulator(MockSolidWorksEnvironment environment)
        {
            _environment = environment;
        }

        public IReadOnlyList<string> InteractionLog => _interactionLog.AsReadOnly();

        public void SimulateButtonClick(string controlId)
        {
            Register($"Button clicked: {controlId}");
        }

        public void SimulateCommand(string commandPath)
        {
            Register($"Command executed: {commandPath}");
        }

        public void SimulateSelection(string entityName)
        {
            Register($"Entity selected: {entityName}");
        }

        public void Clear()
        {
            _interactionLog.Clear();
        }

        private void Register(string message)
        {
            var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _interactionLog.Insert(0, entry);
            if (_interactionLog.Count > 200)
            {
                _interactionLog.RemoveAt(_interactionLog.Count - 1);
            }

            _environment.Logger.Log(message);
        }
    }
}
