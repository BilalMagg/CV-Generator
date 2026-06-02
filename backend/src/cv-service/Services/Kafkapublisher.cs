using Confluent.Kafka;
using CvService.Events;

public interface IKafkaPublisher{
 Task PublishAsync<T>(T evt, string topic ) where T: CVEvent;
}

public class KafkaPublisher : IKafkaPublisher, IDisposable
{
  private readonly IProducer<string, string> _producer;
  private readonly ILogger<KafkaPublisher> _logger;

  public KafkaPublisher(IConfiguration configuration, ILogger<KafkaPublisher>  logger)
  {
    this._logger = logger;
  }
  public Task PublishAsync <CVEvent>(CVEvent cVEvent, string topic)
  {
    
  }


}


