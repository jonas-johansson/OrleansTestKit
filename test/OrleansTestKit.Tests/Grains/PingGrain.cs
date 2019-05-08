using System.Threading.Tasks;
using Orleans;
using TestInterfaces;

namespace TestGrains
{
    public class PingState
    {
        public int Value { get; private set; }
    }

    public class PingGrain : Grain<PingState>, IPing
    {
        public Task Ping()
        {
            var pong = GrainFactory.GetGrain<IPong>(22);

            return pong.Pong();
        }

        public Task PingCompound()
        {
            var pong = GrainFactory.GetGrain<IPongCompound>(44, keyExtension: "Test");

            return pong.Pong();
        }
    }
}
