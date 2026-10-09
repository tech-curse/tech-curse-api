using TechCurse.Api.IntegrationTests.Infraestrutura;
using Xunit.Sdk;
using Xunit.v3;

[assembly: AssemblyFixture(typeof(AmbienteDeTeste))]
[assembly: Parallelization(Mode = ParallelMode.None)]
