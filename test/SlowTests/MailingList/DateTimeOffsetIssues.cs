using System;
using FastTests;
using Sparrow;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.MailingList
{
    public class DateTimeOffsetIssues : RavenTestBase
    {
        public DateTimeOffsetIssues(ITestOutputHelper output) : base(output)
        {
        }

        private class Book
        {
            public string Id { get; set; }
            public string Author { get; set; }
        }

        [Theory]
        [InlineData("2011-11-05T13:09:17.5402774")]
        [InlineData("2011-11-05T13:09:17.540277")]

        public void Adding_DateTimeOffset_To_metadata_should_fetch_it_as_DateTimeOffset(string expectedDateTimeString)
        {
            using (var store = GetDocumentStore())
            {

                // Arrange
                string bookId;
                var expectedDateTime = DateTimeOffset.Parse(expectedDateTimeString);

                using (var session = store.OpenSession())
                {
                    var entity = new Book { Author = "dane" };
                    session.Store(entity);
                    session.SaveChanges();
                    bookId = entity.Id;
                }

                // Act
                // Add metadata to the entity
                using (var session = store.OpenSession())
                {
                    var book = session.Load<Book>(bookId);
                    var metadata = session.Advanced.GetMetadataFor(book);
                    metadata.Add("DateTime-ToCheck", expectedDateTime.ToString(DefaultFormat.DateTimeOffsetFormatsToWrite));
                    session.SaveChanges();
                }

                // Try get metadata
                using (var session = store.OpenSession())
                {
                    var entity = session.Load<Book>(bookId);
                    var metadata = session.Advanced.GetMetadataFor(entity);

                    string offset = metadata.GetString("DateTime-ToCheck");
                    var result = DateTimeOffset.Parse(offset); // No exception is thrown here
                    Assert.IsType<DateTimeOffset>(result);
                    Assert.Equal(expectedDateTime, result);
                }

                // Change the entity
                using (var session = store.OpenSession())
                {
                    var book = session.Load<Book>(bookId);
                    book.Author = "Jane Doe";
                    session.SaveChanges();
                }

                // Assert
                // Try to get the metadata back as DateTime
                using (var session = store.OpenSession())
                {
                    var entity = session.Load<Book>(bookId);
                    var metadata = session.Advanced.GetMetadataFor(entity);

                    string offset = metadata.GetString("DateTime-ToCheck");
                    var result = DateTimeOffset.Parse(offset); // An exception should not be thrown here, after changing the entity
                    Assert.Equal(expectedDateTime, result);
                }
            }
        }


        [Theory]
        [InlineData("2011-11-05T13:09:17.5402774")]
        [InlineData("2011-11-05T13:09:17.540277")]
        public void Adding_DateTime_to_metadata_should_fetch_it_as_DateTime(string expectedDateTimeString)
        {
            using (var _documentStore = GetDocumentStore())
            {

                // Arrange
                string bookId;
                var expectedDateTime = DateTime.Parse(expectedDateTimeString);

                using (var session = _documentStore.OpenSession())
                {
                    var entity = new Book { Author = "dane" };
                    session.Store(entity);
                    session.SaveChanges();
                    bookId = entity.Id;
                }

                // Act
                // Add metadata to the entity
                using (var session = _documentStore.OpenSession())
                {
                    var book = session.Load<Book>(bookId);
                    var metadata = session.Advanced.GetMetadataFor(book);
                    metadata.Add("DateTime-ToCheck", expectedDateTime.ToString(DefaultFormat.DateTimeOffsetFormatsToWrite));
                    session.SaveChanges();
                }

                // Try get metadata
                using (var session = _documentStore.OpenSession())
                {
                    var entity = session.Load<Book>(bookId);
                    var metadata = session.Advanced.GetMetadataFor(entity);
                    string offset = metadata.GetString("DateTime-ToCheck");
                    var result = DateTime.Parse(offset); // No exception is thrown here
                    Assert.IsType<DateTime>(result);
                    Assert.Equal(expectedDateTime, result);
                }

                // Change the entity
                using (var session = _documentStore.OpenSession())
                {
                    var book = session.Load<Book>(bookId);
                    book.Author = "Jane Doe";
                    session.SaveChanges();
                }

                // Assert
                // Try to get the metadata back as DateTime
                using (var session = _documentStore.OpenSession())
                {
                    var entity = session.Load<Book>(bookId);
                    var metadata = session.Advanced.GetMetadataFor(entity);

                    string offset = metadata.GetString("DateTime-ToCheck");
                    var result = DateTime.Parse(offset); // An exception should not be thrown here, after changing the entity
                    Assert.Equal(expectedDateTime, result);
                }
            }
        }

    }
}
