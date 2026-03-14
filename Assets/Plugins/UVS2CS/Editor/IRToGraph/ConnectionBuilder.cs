using Unity.VisualScripting;

namespace UVS2CS.IRToGraph
{
    public sealed class ConnectionBuilder
    {
        readonly FlowGraph _graph;

        public ConnectionBuilder(FlowGraph graph)
        {
            _graph = graph;
        }

        public void ConnectControl(ControlOutput source, ControlInput destination)
        {
            if (source == null || destination == null) return;
            _graph.controlConnections.Add(new ControlConnection(source, destination));
        }

        public void ConnectValue(ValueOutput source, ValueInput destination)
        {
            if (source == null || destination == null) return;
            _graph.valueConnections.Add(new ValueConnection(source, destination));
        }
    }
}
