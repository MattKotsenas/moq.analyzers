// See https://aka.ms/new-console-template for more information
using Moq;
//using M = Moq.MockBehavior;

Console.WriteLine("Hello, World!");

new Mock<ISample>(MockBehavior.Loose);

_ = Mock.Of<ISample>(MockBehavior.Strict);

interface ISample
{
    int Calculate() => 0;
}
