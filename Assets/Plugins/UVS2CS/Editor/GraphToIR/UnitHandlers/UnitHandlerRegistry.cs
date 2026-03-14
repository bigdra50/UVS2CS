using System;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class UnitHandlerRegistry
    {
        readonly List<IUnitHandler> _handlers = new();

        public void Register(IUnitHandler handler) => _handlers.Add(handler);

        public IUnitHandler GetHandler(IUnit unit)
        {
            foreach (var handler in _handlers)
            {
                if (handler.CanHandle(unit))
                    return handler;
            }
            return null;
        }

        public static UnitHandlerRegistry CreateDefault()
        {
            var registry = new UnitHandlerRegistry();
            registry.Register(new LiteralHandler());
            registry.Register(new NullHandlers());
            registry.Register(new CustomEventHandlers());
            registry.Register(new EventHandlers());
            registry.Register(new ControlFlowHandlers());
            registry.Register(new VariableHandlers());
            registry.Register(new MemberHandlers());
            registry.Register(new LogicHandlers());
            registry.Register(new MathHandlers());
            registry.Register(new TimeHandlers());
            registry.Register(new CollectionHandlers());
            registry.Register(new NestingHandlers());
            return registry;
        }
    }
}
