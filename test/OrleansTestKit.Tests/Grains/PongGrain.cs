using System.Threading.Tasks;
using Orleans;
using TestInterfaces;

namespace TestGrains
{
    public class PongState
    {
        public string Value { get; private set; }
    }

    public class PongGrain : Grain<PongState>, IPong
    {
        public bool PongWasCalled { get; set; }

        public Task Pong()
        {
            PongWasCalled = true;
            return Task.CompletedTask;
        }
    }
}
