using System;

namespace Trupe.Factories;

public interface IActorFactory
{
    IActor CreateActor(Type actorType);
}
