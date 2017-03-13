class PdfMaker < Middleman::Extension
  def initialize(app, options_hash={}, &block)
    super
  end

  def after_build(builder)
    begin
      require 'pdfkit'

      margin_top = margin_bottom = margin_left = margin_right = '2.54cm'

      if Gem.win_platform?
        PDFKit.configure do |config|
          # Full path is C:\Program Files\wkhtmltopdf\bin\wkhtmltopdf.exe
          # but the space trips up PDFKit. Rely on the bin folder being
          # in the path
          config.wkhtmltopdf = 'wkhtmltopdf.exe'
        end
      end

      # dpi must be 300, and disable_smart_shrinking must be true,
      # for the A4 size to actually be set
      kit = PDFKit.new(File.new('build/pdf.html'),
                       :page_size => 'A4',
                       :margin_top => margin_top,
                       :margin_bottom => margin_bottom,
                       :margin_left => margin_left,
                       :disable_smart_shrinking => true,
                       :margin_right => margin_right,
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

