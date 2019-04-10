using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace handlers.SSM.net.Tests
{
    [TestFixture]
    public class AwsSsmHandlerTests
    {
        [Test]
        public void IsAwsRegion_Invalid_Aws_Region_Returns_False()
        {
            var result = AwsSsmHandler.IsAwsRegion("XXX");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsAwsRegion_Valid_Aws_Region_Returns_True()
        {
            var result = AwsSsmHandler.IsAwsRegion("eu-west-1");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void IsValidCommandType_Invalid_Command_Type_Returns_False()
        {
            var result = AwsSsmHandler.IsValidCommandType("XXX");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsValidCommandType_Valid_Command_Type_Returns_True()
        {
            var result = AwsSsmHandler.IsValidCommandType("get-command-invocation");
            Assert.IsTrue(result);
            result = AwsSsmHandler.IsValidCommandType("send-command");
            Assert.IsTrue(result);
        }

        [Test]
        public void ValidateRequest_Empty_Request_Throws_Exception()
        {
            // Act
            Exception ex = Assert.Throws<Exception>(() => AwsSsmHandler.ValidateRequest(null));

            // Assert
            Assert.That(ex.Message, Is.EqualTo("Request cannot be null or empty."));
        }

        [Test]
        public void ValidateRequest_Empty_Instance_Id_Throws_Exception()
        {
            // Arrange
            UserRequest request = new UserRequest();

            // Act
            Exception ex = Assert.Throws<Exception>(() => AwsSsmHandler.ValidateRequest(request));

            // Assert
            Assert.That(ex.Message, Is.EqualTo("Instance id cannot be null or empty."));
        }

        [Test]
        public void ValidateRequest_Invalid_Command_Type_Throws_Exception()
        {
            // Arrange
            UserRequest request = new UserRequest()
            {
                InstanceId = "i-xxxxxxxx"
            };

            // Act
            Exception ex = Assert.Throws<Exception>(() => AwsSsmHandler.ValidateRequest(request));

            // Assert
            Assert.That(ex.Message, Is.EqualTo("Command type cannot be null or empty."));
        }

        [Test]
        public void ValidateRequest_Empty_Command_Id_For_Get_Command_Invocation_Throws_Exception()
        {
            // Arrange
            UserRequest request = new UserRequest()
            {
                InstanceId = "i-xxxxxxxx",
                CommandType = "get-command-invocation"
            };

            // Act
            Exception ex = Assert.Throws<Exception>(() => AwsSsmHandler.ValidateRequest(request));

            // Assert
            Assert.That(ex.Message, Is.EqualTo("Command id cannot be null or empty for 'get-command-invocation'."));
        }

        [Test]
        public void ValidateRequest_Empty_Command_Document_For_Send_Command_Throws_Exception()
        {
            // Arrange
            UserRequest request = new UserRequest()
            {
                InstanceId = "i-xxxxxxxx",
                CommandType = "send-command"
            };

            // Act
            Exception ex = Assert.Throws<Exception>(() => AwsSsmHandler.ValidateRequest(request));

            // Assert
            Assert.That(ex.Message, Is.EqualTo("Command document cannot be null or empty for 'send-command'."));
        }

        [Test]
        public void ValidateRequest_Invalid_Aws_Region_Throws_Exception()
        {
            // Arrange
            UserRequest request = new UserRequest()
            {
                InstanceId = "i-xxxxxxxx",
                CommandType = "send-command",
                CommandDocument = "xxxxxxxx"
            };

            // Act
            Exception ex = Assert.Throws<Exception>(() => AwsSsmHandler.ValidateRequest(request));

            // Assert
            Assert.That(ex.Message, Is.EqualTo("AWS region specified is not valid."));
        }

        [Test]
        public void ValidateRequest_Empty_Aws_Role_Throws_Exception()
        {
            // Arrange
            UserRequest request = new UserRequest()
            {
                InstanceId = "i-xxxxxxxx",
                CommandType = "send-command",
                CommandDocument = "xxxxxxxx",
                AwsRegion = "eu-west-1"
            };

            // Act
            Exception ex = Assert.Throws<Exception>(() => AwsSsmHandler.ValidateRequest(request));

            // Assert
            Assert.That(ex.Message, Is.EqualTo("AWS role cannot be null or empty."));
        }
    }
}
