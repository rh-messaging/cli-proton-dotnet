# cli-proton-dotnet

cli-proton-dotnet is a collection of command-line messaging clients built on [Apache Qpid Proton DotNet](https://github.com/apache/qpid-proton-dotnet) amqp library.

## Installation

cli-proton-dotnet requires [.NET](https://dotnet.microsoft.com/) v6.0+ to run.

```sh
git clone https://github.com/rh-messaging/cli-proton-dotnet.git
cd cli-proton-dotnet
dotnet build
```

## Using

### Using command-line client

```sh
Sender --broker "username:password@localhost:5672" --address "queue_test" --count 2 --msg-content "text message" --log-msgs dict
Receiver --broker "username:password@localhost:5672" --address "queue_test" --count 2 --log-msgs dict
```

## Related projects

* https://github.com/rh-messaging/cli-netlite
* https://github.com/rh-messaging/cli-java
* https://github.com/rh-messaging/cli-rhea
* https://github.com/rh-messaging/cli-proton-python
* https://github.com/rh-messaging/cli-proton-ruby

## License

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
