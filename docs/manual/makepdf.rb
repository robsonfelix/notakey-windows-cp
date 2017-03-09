class PdfMaker < Middleman::Extension
  def initialize(app, options_hash={}, &block)
    super
  end

  def after_build(builder)
    begin
      require 'pdfkit'

      kit = PDFKit.new(File.new('build/pdf.html'),
                       :page_size => 'A4',
                       :margin_top => 10,
                       :margin_bottom => 10,
                       :margin_left => 10,
                       :margin_right => 10,
                       :disable_smart_shrinking => false,
                       :print_media_type => true,
                       :dpi => 300
      )

      file = kit.to_file('build/output.pdf')

    rescue Exception =>e
      builder.thor.say_status "PDF Maker",  "Error: #{e.message}", Thor::Shell::Color::RED
      raise
    end

    builder.thor.say_status "PDF Maker",  "PDF file available at build/output.pdf"
  end

end

::Middleman::Extensions.register(:pdfmaker, PdfMaker)

