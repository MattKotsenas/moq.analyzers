// See https://aka.ms/new-console-template for more information
using Moq;
using M = Moq.MockBehavior;

Console.WriteLine("Hello, World!");

_ = Mock.Of<ISample>(global::Moq.MockBehavior.Loose);

interface ISample
{
    int Calculate() => 0;
}
