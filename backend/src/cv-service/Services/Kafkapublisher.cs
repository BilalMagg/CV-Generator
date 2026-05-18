using CvService.Events;

public interface IKafkaPublisher{
 Task PublishAsync<T>(T evt, string topic ) where T: CVEvent;
}


